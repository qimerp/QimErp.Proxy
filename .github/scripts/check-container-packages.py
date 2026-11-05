import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request


# These DEV services had no image package in the authenticated org inventory.
KNOWN_UNPUBLISHED = {
    "qimerp-platform-api",
    "qimerp-corehr-api",
    "qimerp-performance-api",
    "qimerp-learning-api",
    "qimerp-payroll-core-api",
    "qimerp-finance-api",
    "qimerp-platform-reporting-api",
    "qimerp-platform-reporting-worker",
    "qimerp-proxy-mobile-api",
}


EXPECTED_PACKAGES = {
    "qimerp/qimerp.platform": {"qimerp-platform-api"},
    "qimerp/qimerp.corehr": {"qimerp-corehr-api"},
    "qimerp/qimerp.performance": {"qimerp-performance-api"},
    "qimerp/qimerp.learning": {"qimerp-learning-api"},
    "qimerp/qimerp.payroll": {"qimerp-payroll-core-api"},
    "qimerp/qimerp.finance": {"qimerp-finance-api"},
    "qimerp/qimerp.operations": {"qimerp-operations-api"},
    "qimerp/qimerp.core.reporting": {"qimerp-platform-reporting-api", "qimerp-platform-reporting-worker"},
    "qimerp/qimerp.proxy": {"qimerp-proxy-mobile-api"},
}


def get_json(url, token):
    request = urllib.request.Request(
        url,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        try:
            payload = json.loads(error.read(4096))
            reason = payload.get("message") if isinstance(payload, dict) else None
        except (OSError, ValueError, TypeError, AttributeError):
            reason = None
        if not isinstance(reason, str):
            reason = "Response body did not contain a JSON error message"
        path = urllib.parse.urlsplit(url).path
        message = f"GitHub package API returned HTTP {error.code} at {path}: {reason}"
        if token:
            message = message.replace(token, "[REDACTED]")
        message = re.sub(r"(?i)(?:gh[pousr]_[a-z0-9_]+|github_pat_[a-z0-9_]+)", "[REDACTED]", message)
        message = re.sub(r"[\x00-\x1f\x7f-\x9f]", "", message)
        if len(message) > 300:
            message = message[:297] + "..."
        raise RuntimeError(message) from error


def available_packages(repository, packages, token):
    expected = EXPECTED_PACKAGES.get(repository.casefold())
    if expected is None or not set(packages).issubset(expected):
        raise RuntimeError("No container package policy matches this repository and package selection")
    owner, name = repository.split("/", 1)
    identity_url = f"https://api.github.com/repos/{urllib.parse.quote(owner, safe='')}/{urllib.parse.quote(name, safe='')}"
    identity = get_json(identity_url, token)
    full_name = identity.get("full_name") if isinstance(identity, dict) else None
    if not isinstance(full_name, str) or full_name.casefold() != repository.casefold():
        raise RuntimeError("Unexpected repository identity from GitHub package access check")

    root = f"https://api.github.com/orgs/{urllib.parse.quote(owner, safe='')}/packages/container"
    results = []
    for package in packages:
        url = f"{root}/{urllib.parse.quote(package, safe='')}"
        try:
            metadata = get_json(url, token)
        except RuntimeError as error:
            cause = error.__cause__
            if (
                isinstance(cause, urllib.error.HTTPError)
                and cause.code == 404
                and package in KNOWN_UNPUBLISHED
            ):
                results.append((package, False))
                continue
            raise
        if not isinstance(metadata, dict) or metadata.get("name") != package:
            raise RuntimeError(f"Unexpected metadata for container package {package}")
        association = metadata.get("repository")
        associated_name = association.get("full_name") if isinstance(association, dict) else None
        if not isinstance(associated_name, str) or associated_name.casefold() != repository.casefold():
            raise RuntimeError(f"Container package {package} is not associated with the expected repository")
        results.append((package, True))
    return results


def main():
    packages = sys.argv[1:]
    if not packages:
        raise RuntimeError("Specify at least one expected container package")
    results = available_packages(os.environ["GITHUB_REPOSITORY"], packages, os.environ["GH_TOKEN"])
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        for index, (package, exists) in enumerate(results, start=1):
            output.write(f"image_{index}={str(exists).lower()}\n")
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
        for package, exists in results:
            if exists:
                summary.write(f"- `{package}` is readable; keep its newest 10 versions.\n")
            else:
                summary.write(f"- Known-unpublished `{package}` is unavailable; no image pruning was performed.\n")


if __name__ == "__main__":
    main()

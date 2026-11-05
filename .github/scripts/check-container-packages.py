import json
import os
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
        raise RuntimeError(f"GitHub package API returned HTTP {error.code}") from error


def available_packages(owner, packages, token):
    root = f"https://api.github.com/orgs/{urllib.parse.quote(owner, safe='')}/packages"
    listed = set()
    page = 1
    while True:
        entries = get_json(f"{root}?package_type=container&per_page=100&page={page}", token)
        if not isinstance(entries, list):
            raise RuntimeError("GitHub package listing was not an array")
        listed.update(entry["name"] for entry in entries)
        if len(entries) < 100:
            break
        page += 1

    results = []
    for package in packages:
        url = f"{root}/container/{urllib.parse.quote(package, safe='')}"
        try:
            metadata = get_json(url, token)
        except RuntimeError as error:
            cause = error.__cause__
            if (
                isinstance(cause, urllib.error.HTTPError)
                and cause.code == 404
                and package in KNOWN_UNPUBLISHED
                and package not in listed
            ):
                results.append((package, False))
                continue
            raise
        if metadata.get("name") != package:
            raise RuntimeError(f"Unexpected metadata for container package {package}")
        results.append((package, True))
    return results


def main():
    packages = sys.argv[1:]
    if not packages:
        raise RuntimeError("Specify at least one expected container package")
    results = available_packages(os.environ["GITHUB_REPOSITORY_OWNER"], packages, os.environ["GH_TOKEN"])
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

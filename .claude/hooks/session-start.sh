#!/bin/bash
#
# Makes a cloud session able to build and test this repo.
#
# Written because a container came back without the .NET SDK at all and the
# session could not compile a line until it was installed by hand. The official
# installer at dot.net is no use here: it redirects to
# builds.dotnet.microsoft.com, which the environment's network policy denies, so
# the download completes with an empty file and `dotnet` is still missing. The
# distribution packages are reachable, so that is what this uses.
#
set -euo pipefail

# Local machines have their own toolchain and their own opinions about it.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Idempotent: the container image is cached after this runs, so most sessions
# find the SDK already here and do nothing.
#
# The test is for an SDK and not for the `dotnet` command. Removing
# dotnet-sdk-8.0 leaves the host and the runtime behind, so /usr/bin/dotnet is
# still on PATH and still exits 0 while `dotnet build` reports that the
# application 'build' does not exist. A `command -v dotnet` guard reads that as
# "already installed" and leaves the session unable to compile a line.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  apt-get install -y -qq --no-install-recommends dotnet-sdk-8.0
fi

dotnet --version

# Warm the package cache so the first build in the session is not also the
# first restore. Failure here is not fatal — a session that can compile is
# worth having even if the restore has to happen later.
#
# `set -u` turns an unset CLAUDE_PROJECT_DIR into a crash rather than a missing
# restore, so the repo root is worked out from this script's own location when
# nothing hands it over.
repo="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
dotnet restore "$repo/WrestlingSim.sln" || true

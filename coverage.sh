#!/usr/bin/env bash
# Runs the full test suite with the >95% coverage gate (line + branch) enforced.
#
# Each test project measures only its module-under-test (via <Include> in its .csproj)
# and fails the build if line or branch coverage drops to 95% or below. The Host
# composition root and code marked [ExcludeFromCodeCoverage] are excluded. See CLAUDE.md.
#
# Usage: ./coverage.sh
set -euo pipefail

dotnet test -p:CollectCoverage=true

echo
echo "Coverage gate passed: line + branch > 95% on Core, Infrastructure, and Bot."

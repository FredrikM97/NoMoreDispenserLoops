#!/usr/bin/env bash
set -euo pipefail
if [[ -z "$STEAM_USERNAME" || -z "$STEAM_CONFIG_VDF" ]]; then
  echo "::error::Configure the STEAM_USERNAME and STEAM_CONFIG_VDF secrets."
  exit 1
fi

"""Adds a release to manifest.json: update_manifest.py <version> <zip> <github-repo>."""
import datetime
import hashlib
import json
import re
import sys
from pathlib import Path

version, zip_path, repo = sys.argv[1:4]
root = Path(__file__).resolve().parents[2]
manifest_path = root / "manifest.json"

csproj = (root / "Jellyfin.Plugin.OnePaceStreams" / "Jellyfin.Plugin.OnePaceStreams.csproj").read_text()
jellyfin_version = re.search(r'Include="Jellyfin\.Controller" Version="([\d.]+)"', csproj).group(1)
target_abi = ".".join((jellyfin_version.split(".") + ["0"] * 4)[:4])

entry = {
    "version": version,
    "changelog": f"https://github.com/{repo}/releases/tag/v{version}",
    "targetAbi": target_abi,
    "sourceUrl": f"https://github.com/{repo}/releases/download/v{version}/{Path(zip_path).name}",
    "checksum": hashlib.md5(Path(zip_path).read_bytes()).hexdigest(),
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}

manifest = json.loads(manifest_path.read_text())
versions = manifest[0]["versions"]
versions[:] = [v for v in versions if v["version"] != version]
versions.insert(0, entry)
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps(entry, indent=2))

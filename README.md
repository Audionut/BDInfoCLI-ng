# BDInfoCLI-ng

Forked and modified version of https://github.com/rokibhasansagar/BDInfoCLI-ng

BDInfoCLI-ng is the latest BDInfo (with UHD support) modified for use as a CLI utility. BDInfoCLI-ng implements an interface similar to BDInfoCLI, but on the latest BDInfo code base and with code changes designed to be as minimally invasive as possible for easier maintainability with BDInfo updates.

## Usage

```
Usage: BDInfo.exe <BD_PATH> [REPORT_DEST]
BD_PATH may be a directory containing a BDMV folder or a BluRay ISO file.
REPORT_DEST is the folder the BDInfo report is to be written to. If not
given, the report will be written to BD_PATH. REPORT_DEST is required if
BD_PATH is an ISO file.

  -?, --help, -h             Print out the options.
  -l, --list                 Print the list of playlists.
  -m, --mpls=VALUE           Comma separated list of playlists to scan.
  -w, --whole                Scan whole disc - every playlist.
  -v, --version              Print the version.
```

### Examples

```
# Display playlists in given disc, prompt user to select playlists
# to scan, and output the generated report to the same disc path:
# (If an ISO file is given, then a REPORT_DEST must be given as well. See next example.)
BDInfo.exe BD_PATH

# Same as above, but output report in given report folder:
# (If BD_PATH is an ISO, these are the minimum arguments required)
BDInfo.exe BD_PATH REPORT_OUTPUT_DIR

# Just display the list of playlists in the given disc:
BDInfo.exe -l BD_PATH

# Scan the whole disc (every playlist) and write report to disc folder (non-interactive):
BDInfo.exe -w BD_PATH

# Scan selected playlists and write report to disc folder (non-interactive):
BDInfo.exe -m 00006.MPLS,00009.MPLS BD_PATH

# Display the BDInfo version this build of BDInfoCLI-ng is based on:
BDInfo.exe -v
```

## Notes
This version is predominantly built for https://github.com/Audionut/Upload-Assistant which uses it's own playlist detection, and only runs on playlists.
It may or may not work in other uses, as other use cases are never tested. Must have an argument supplied.
Requires a x64 architecture, with windows/linux (and docker)/mac binaries being published.

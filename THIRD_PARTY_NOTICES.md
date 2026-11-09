# Third-party notices

## ffmpeg

The release package includes `ffmpeg.exe` built by
[gyan.dev](https://www.gyan.dev/ffmpeg/builds/) (ffmpeg essentials build).

ffmpeg is licensed under the GNU General Public License (GPL). The corresponding
source code is available at <https://ffmpeg.org/download.html> and
<https://git.ffmpeg.org/ffmpeg.git>.

The application calls ffmpeg as a separate process for audio trimming,
concatenation and MP3 encoding. It does not link against ffmpeg libraries.

If you redistribute the release package, keep this notice and comply with the
GPL for the bundled ffmpeg binary.

## .NET 8

The self-contained executable bundles the .NET 8 runtime, licensed under the
MIT License. See <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>.

## Noto / system fonts

The UI uses the system font `Microsoft YaHei UI` on Chinese Windows and falls
back to the system default font on other systems. No font files are bundled.

# AUTO4W

A C# Windows Forms rebuild scaffold for the original `4Wsadaqa.exe`, designed around a compact interface and user-editable settings.

## Build with AppVeyor
1. Upload the **contents** of this ZIP to a GitHub repository (extract the ZIP first; don't upload only the ZIP as a repository file).
2. Connect the repository to AppVeyor.
3. AppVeyor detects `appveyor.yml`, builds `AUTO4W.sln`, and publishes `AUTO4W.exe` as an artifact.

## Customization available in this starter
- Editable Incoming, Toprint, Digital, and Errors folder paths.
- Enable/disable feature modules.
- Six editable schedule groups.
- Photo size/layout preference fields.
- Old/new extension settings.
- Theme/accent and startup preferences.
- Settings saved locally in `%AppData%\\AUTO4W\\settings.json`.
- Basic extension replacement operation with preview/confirmation.
- Activity log.

## Important
This is the foundation for the rebuild, not yet a verified 1:1 replacement of the original. The original executable reveals interface names but not all original processing rules. The 4-Wallets image processing, original scheduling semantics, Auto Error Fix, and Order Separation rules must be verified against examples before their behavior can be considered equivalent. This starter keeps those settings configurable and separates the modules so the logic can be implemented safely as it is verified.

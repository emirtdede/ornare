# Ornare V1 User Guide

Ornare is a minimalist, local-first Windows 11 utility designed to turn images into durable folder icons.

---

## 1. Installation

1. Download the signed `Ornare_1.0.0.0_x64.msix` package.
2. Double-click the file to install through the Windows App Installer, or run via PowerShell:
   ```powershell
   Add-AppxPackage -Path .\Ornare_1.0.0.0_x64.msix
   ```
3. Once installed, Ornare automatically integrates into the Windows 11 File Explorer context menu.

---

## 2. Setting a Folder Icon

1. In Windows 11 File Explorer, right-click any standard folder.
2. Click **Set Folder Icon** (or **Klasör Simgesini Ayarla** in Turkish).
3. The Ornare Quick Panel will appear:
   - **Drag and Drop**: Drag an image (PNG, JPEG, WebP, BMP, GIF, TIFF, ICO) directly onto the panel.
   - **Browse**: Click the drop area to open the file picker.
   - **Paste**: Copy an image or image file to the clipboard, then press `Ctrl+V` or click **Paste Image**.
   - **Recent / Favorites**: Select any previously used icon from the horizontal strip.
4. The folder icon updates immediately in File Explorer.

---

## 3. Undo and Reset

- **Undo**: If you customize an icon and wish to revert to the immediately preceding state, open the Quick Panel and click **Undo** (or press `Ctrl+Z`).
- **Reset**: To completely remove all Ornare customizations and restore the folder's original appearance, click **Reset**. Unrelated `desktop.ini` settings (such as folder view modes or descriptions) are carefully preserved.

---

## 4. Manager Application

Open **Ornare** from the Windows Start menu to access the Manager:
- **Library**: Browse all previously created icons. Click any icon to view details, toggle favorite status, or delete an unassigned icon. Real-time search allows quick filtering.
- **Recent**: View your history of applied folder customizations.
- **Favorites**: Quickly access your starred icons.
- **Customized Folders**: View all currently customized folders with their health status (Healthy, Needs Attention, External Conflict).
- **Repair**: If a folder's icon was disrupted (for example, missing hidden/system attributes or an orphaned icon file), click **Repair** to restore proper configuration without overwriting external customizations.
- **Settings**: Change the application theme (System / Light / Dark) and language (System / English / Turkish).

---

## 5. Diagnostic Export

If you encounter an issue:
1. Navigate to **Settings** in the Manager window.
2. Click **Export Diagnostic Report**.
3. A JSON report is saved containing system architecture, customization counts, and recent error messages.
4. **Privacy Protection**: All user profile paths (e.g. `C:\Users\username\...`) are automatically redacted before export.

---

## 6. Uninstall Behavior & User Data Ownership

- **Your Data Stays Yours**: Uninstalling Ornare does not delete your customized folders or remove the icons inside them.
- Because icons are stored locally inside each customized folder (`.ornare\folder.ico`), Windows Explorer continues to render your custom icons even after Ornare is uninstalled.

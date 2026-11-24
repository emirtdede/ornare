# Privacy Policy

Ornare is published by Vellium Teknoloji Anonim Şirketi. This policy describes what the
application does with your information. It is written to be read, not to protect the publisher.

## Summary

- Ornare has no account and no sign-in.
- Ornare collects no telemetry, analytics, advertising identifiers or crash reports.
- Ornare makes no network request of its own. There is no server operated by the publisher.
- Everything Ornare knows about you stays on the device, in files this policy names.

## What the application stores

| Data | Where it is stored | Why |
| :--- | :--- | :--- |
| Folder assignments, collections, tags, favourites | The local application database in your user profile | To show, repair, reset and undo what you applied |
| Settings such as language, theme, accent colour and interface scale | The same local database | To restore your preferences on the next launch |
| History of applied operations | The same local database | To let you undo a change |
| The folder icon Ornare wrote | A hidden `.ornare` subfolder inside the folder you customised | To keep the icon working when the folder is moved or renamed |
| Optional backup snapshot | The folder you choose when you enable backup | To restore your setup on another device |
| Diagnostic report | A file you choose when you export it | To help you or your administrator investigate a problem |

The application database lives in the package data folder that Windows assigns to Ornare in
your user profile. Nothing in it leaves the device unless you copy it somewhere yourself.

## Optional backup

Backup is off until you enable it and choose a destination folder. When it is on, Ornare writes
a single JSON file, with a previous copy and daily copies, into a subfolder named `Ornare`
inside the folder you chose. If that folder is synchronised by a service you already use, that
service will upload the file under its own terms. Ornare does not contact that service, does
not hold credentials for it and cannot read what the service does with the file.

Backup snapshots contain portable settings, folder and collection metadata, and a redacted
folder label rather than a full personal path. Binary icon caches stay on the device.

## Diagnostics

Ornare can export a diagnostic report on request. The report is written only when you ask for
it, it is saved where you choose, and personal paths are anonymised. Diagnostic logging is
off in a normal session and is enabled only by setting the environment variable
`ORNARE_DIAGNOSTIC_SESSION=1`, which is an action a user or an administrator takes
deliberately.

## What Ornare never does

- It does not create an account or ask for an email address.
- It does not send telemetry, usage statistics or crash dumps anywhere.
- It does not upload your images, folder names or folder paths.
- It does not include advertising, and it does not profile you.
- It does not read files outside the folders you selected and its own data folder.

## Your control over the data

- **Undo and Reset** in the application revert an icon without deleting your files.
- **Backup** is opt-in and writes only to the folder you choose; turning it off stops further
  writes, and the files already written remain yours to delete.
- **Diagnostics** are exported only when you ask, to a location you choose.
- **Uninstalling Ornare** removes the application and its package data folder, which holds the
  application database. Folder icons already applied stay in place unless you reset them,
  because the product never deletes files it did not create without your instruction.

## Children

Ornare is a general-purpose desktop customisation tool. It is not directed at children and it
collects no personal information from anyone.

## Changes to this policy

A change is published in the repository and in the release notes for the version that changes
the behaviour. The application does not change what it stores without a release note.

## Contact

Questions about this policy: open an issue at
[github.com/emirtdede/ornare](https://github.com/emirtdede/ornare) or use the security
disclosure route in [SECURITY.md](SECURITY.md).

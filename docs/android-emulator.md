# Running on an Android emulator

Verified on Windows 11, Android 15 (API 35), Expo Go 57.0.9, against the
deployed API. No Android Studio: the command-line SDK is enough and leaves
several hundred MB of RAM that the IDE would otherwise take.

## One-time setup

Already done on this machine; here for a fresh one.

```powershell
winget install Microsoft.OpenJDK.17          # sdkmanager is a Java tool
winget install OpenJS.NodeJS.LTS             # RN 0.86 needs 20.19.4+/22.13+/24.3+
```

Then the SDK itself, into `%LOCALAPPDATA%\Android\Sdk`:

```powershell
# Get the current URL from https://developer.android.com/studio ("Command line
# tools only"), unzip it so that sdkmanager.bat ends up at
#   %LOCALAPPDATA%\Android\Sdk\cmdline-tools\latest\bin\
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
& "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat" --licenses
& "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat" `
    "platform-tools" "emulator" "platforms;android-35" `
    "system-images;android-35;google_apis;x86_64"
```

`ANDROID_HOME`, `ANDROID_SDK_ROOT` and the three `bin` directories are set as
user environment variables, so a new terminal picks them up.

### The virtual device

```powershell
avdmanager create avd --name expense_pixel `
    --package "system-images;android-35;google_apis;x86_64" --device pixel_6
```

Then edit `%USERPROFILE%\.android\avd\expense_pixel.avd\config.ini`:

| Key | Value | Why |
|---|---|---|
| `hw.ramSize` | `1536` | See the memory note below |
| `vm.heapSize` | `192` | Scaled to match |
| `hw.gpu.enabled` | `yes` | Created as `no`, which renders nothing usefully |
| `hw.gpu.mode` | `swiftshader_indirect` | CPU rendering; works without a GPU driver |
| `hw.keyboard` | `yes` | Lets `adb shell input text` type into fields |

## Running it

Three things, and **the order matters** — see the memory note.

```powershell
# 1. Metro first, while RAM is free
cd mobile
npx expo start

# 2. The emulator, in a second terminal
emulator -avd expense_pixel -no-boot-anim -no-audio -gpu swiftshader_indirect -memory 1536

# 3. Once it has booted, press `a` in the Metro terminal
```

Pressing `a` installs Expo Go and opens the app. To do it without an
interactive terminal:

```powershell
adb reverse tcp:8081 tcp:8081
adb shell am start -a android.intent.action.VIEW -d "exp://127.0.0.1:8081" host.exp.exponent
```

The first Android bundle takes around 80 seconds (2714 modules). Later ones
are much faster.

### Pointing it at an API

`.env.development.local` is gitignored and wins over `.env.development`:

```
EXPO_PUBLIC_API_BASE_URL=https://<your-service>.onrender.com
```

A native build sends no `Origin` header, so **CORS does not apply** and the app
can call the deployed API directly. Only the web build needs the API's
`Cors:AllowedOrigins` to list its origin.

For a local backend instead, use `http://10.0.2.2:5165` — the emulator's alias
for the host's loopback. `localhost` there means the emulator itself, which is
the usual cause of a first-run "Network Error".

## Memory

This machine has 7.7 GB. The emulator was created with 2 GB, which the emulator
then rounded up to 2.5 GB, and that left ~300 MB free — at which point Metro
could not bundle at all and hung silently after "Using src/app as the root
directory". It never reported an error; it simply stopped making progress.

Hence 1536 MB, and starting Metro before the emulator so the bundler gets its
memory first. If Metro stalls with no output, check free memory before
suspecting the bundler.

## Gotchas

- **Expo Go's floating dev-menu button sits over the top-right of the screen**,
  which is exactly where the onboarding "Skip" link is. Taps land on the gear
  instead. Use the "Next" button, or drag the bubble aside.
- **`CI=1` silences the interactive port prompt but also disables Fast Refresh**
  ("Metro is running in CI mode, reloads are disabled"). Only use it for
  automated runs.
- **Port 8081 stays held** if Metro is killed abruptly, and the next start
  prompts for another port — which fails outright when non-interactive.
  `Get-NetTCPConnection -LocalPort 8081 -State Listen` finds the owner.

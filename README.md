[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=nanoframework_nanoFramework.Runtime.InFieldUpdate&metric=alert_status)](https://sonarcloud.io/dashboard?id=nanoframework_nanoFramework.Runtime.InFieldUpdate) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=nanoframework_nanoFramework.Runtime.InFieldUpdate&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=nanoframework_nanoFramework.Runtime.InFieldUpdate) [![NuGet](https://img.shields.io/nuget/dt/nanoFramework.Runtime.InFieldUpdate.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.Runtime.InFieldUpdate/) [![#yourfirstpr](https://img.shields.io/badge/first--timers--only-friendly-blue.svg)](https://github.com/nanoframework/Home/blob/main/CONTRIBUTING.md) [![Discord](https://img.shields.io/discord/478725473862549535.svg?logo=discord&logoColor=white&label=Discord&color=7289DA)](https://discord.gg/gCyBu8T)

![nanoFramework logo](https://raw.githubusercontent.com/nanoframework/Home/main/resources/logo/nanoFramework-repo-logo.png)

-----

# Welcome to the .NET **nanoFramework** nanoFramework.Runtime.InFieldUpdate Library repository

This repository contains the nanoFramework.Runtime.InFieldUpdate class library.

## Build status

| Component | Build Status | NuGet Package |
|:-|---|---|
| nanoFramework.Runtime.InFieldUpdate | [![Build Status](https://dev.azure.com/nanoframework/nanoFramework.Runtime.InFieldUpdate/_apis/build/status%2FnanoFramework.Runtime.InFieldUpdate?repoName=nanoframework%2FnanoFramework.Runtime.InFieldUpdate&branchName=main)](https://dev.azure.com/nanoframework/nanoFramework.Runtime.InFieldUpdate/_build/latest?definitionId=130&repoName=nanoframework%2FnanoFramework.Runtime.InFieldUpdate&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/nanoFramework.Runtime.InFieldUpdate.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.Runtime.InFieldUpdate/) |
| nanoFramework.Runtime.InFieldUpdate.Provider | [![Build Status](https://dev.azure.com/nanoframework/nanoFramework.Runtime.InFieldUpdate/_apis/build/status%2FnanoFramework.Runtime.InFieldUpdate?repoName=nanoframework%2FnanoFramework.Runtime.InFieldUpdate&branchName=main)](https://dev.azure.com/nanoframework/nanoFramework.Runtime.InFieldUpdate/_build/latest?definitionId=130&repoName=nanoframework%2FnanoFramework.Runtime.InFieldUpdate&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/nanoFramework.Runtime.InFieldUpdate.Provider.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.Runtime.InFieldUpdate.Provider/) |

## What is In-Field Update

In-Field Update (IFU) lets a deployed device replace its own application (deployment) and nanoCLR
firmware without a debugger or host tool attached.

In the industry this is also commonly called **OTA** (over-the-air) update. The name is a little
misleading: an update can reach a device over Wi-Fi or cellular, but just as well over Ethernet, a
serial link, a USB stick or an SD card. What matters is that the device updates itself in the field,
which is why this library uses the term In-Field Update.

IFU is built on [MCUboot](https://docs.mcuboot.com/):
each image has a *primary* slot that runs and a *secondary* slot where a new image is staged. On
the next reboot MCUboot swaps the new image in as a **test** image; if the new application does not
confirm itself, the previous image is restored on the following reboot.

This library is the managed API to that mechanism: query the images on the device, stage a new one
through an update session (with resume after interruptions), confirm or revert, and reboot. Every
session operation returns an `UpdateSessionResult` saying exactly what happened.

```csharp
// stage a new deployment image, chunk by chunk, as it arrives from the provider
if (UpdateManager.StartUpdateSession(ImageType.Deployment, totalLength, out UpdateSession session)
    != UpdateSessionResult.Success)
{
    return;
}

while (!session.IsComplete)
{
    if (session.Write(NextChunkFromServer()) != UpdateSessionResult.Success)
    {
        return;
    }
}

if (UpdateManager.CompleteUpdateSession(session) == UpdateSessionResult.Success)
{
    UpdateManager.RequestReboot();
}

// ...and in the new application, once it has checked it works:
UpdateManager.ConfirmDeploymentImage();
```

## Documentation

- [Overview](docs/overview.md) - images, slots, update lifecycle and states, API map.
- [Querying images](docs/querying-images.md) - `GetStatus`, `GetPrimaryImageInfo`,
  `GetSecondaryImageInfo`, `GetImageList`, `ImageInfo` and printing them.
- [Update sessions](docs/update-sessions.md) - staging an image, resuming an interrupted download,
  session rules and result codes.
- [Confirm and revert](docs/confirm-and-revert.md) - `ConfirmDeploymentImage`,
  `RequestDeploymentRevert`, `RequestClrRevert`, `RequestReboot` and the test cycle.
- [Writing an update provider](docs/writing-an-update-provider.md) - how to build an update library
  that fetches images from a server or other provider, end to end, on the `IUpdateProvider`
  contract from the `nanoFramework.Runtime.InFieldUpdate.Provider` package.

## Packages

- `nanoFramework.Runtime.InFieldUpdate` - the core API above. All an application needs to stage,
  confirm and revert images.
- `nanoFramework.Runtime.InFieldUpdate.Provider` - the contract for update libraries:
  `IUpdateProvider` (where images come from) and `UpdateAgentOptions` (chunk size, retry cap,
  confirm hook). Pure managed code on top of the core package.

## Samples

- [IFU_Management](Samples/IFU_Management) - lists the images and slots on the device.
- [IFU_SessionUpdate](Samples/IFU_SessionUpdate) - start, pause, resume and complete an update
  session with an image built in RAM.
- [IFU-blink-app](Samples/IFU-blink-app) - a minimal application that confirms itself on boot, to
  build as an update image.

## Feedback and documentation

For documentation, providing feedback, issues and finding out how to contribute please refer to the [Home repo](https://github.com/nanoframework/Home).

Join our Discord community [here](https://discord.gg/gCyBu8T).

## Credits

The list of contributors to this project can be found at [CONTRIBUTORS](https://github.com/nanoframework/Home/blob/main/CONTRIBUTORS.md).

## License

The **nanoFramework** Class Libraries are licensed under the [MIT license](LICENSE.md).

## Code of Conduct

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behaviour in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).

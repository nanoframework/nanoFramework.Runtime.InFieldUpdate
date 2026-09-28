[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=nanoframework_nanoFramework.Runtime.InFieldUpdate&metric=alert_status)](https://sonarcloud.io/dashboard?id=nanoframework_nanoFramework.Runtime.InFieldUpdate) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=nanoframework_nanoFramework.Runtime.InFieldUpdate&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=nanoframework_nanoFramework.Runtime.InFieldUpdate) [![NuGet](https://img.shields.io/nuget/dt/nanoFramework.Runtime.InFieldUpdate.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.Runtime.InFieldUpdate/) [![#yourfirstpr](https://img.shields.io/badge/first--timers--only-friendly-blue.svg)](https://github.com/nanoframework/Home/blob/main/CONTRIBUTING.md) [![Discord](https://img.shields.io/discord/478725473862549535.svg?logo=discord&logoColor=white&label=Discord&color=7289DA)](https://discord.gg/gCyBu8T)

![nanoFramework logo](https://raw.githubusercontent.com/nanoframework/Home/main/resources/logo/nanoFramework-repo-logo.png)

-----

# Welcome to the .NET **nanoFramework** nanoFramework.Runtime.InFieldUpdate Library repository

This repository contains the nanoFramework.Runtime.InFieldUpdate class library.

## Build status

| Component | Build Status | NuGet Package |
|:-|---|---|
| nanoFramework.Runtime.InFieldUpdate | [![Build Status](https://dev.azure.com/nanoframework/nanoFramework.Runtime.InFieldUpdate/_apis/build/status%2FnanoFramework.Runtime.InFieldUpdate?repoName=nanoframework%2FnanoFramework.Runtime.InFieldUpdate&branchName=main)](https://dev.azure.com/nanoframework/nanoFramework.Runtime.InFieldUpdate/_build/latest?definitionId=130&repoName=nanoframework%2FnanoFramework.Runtime.InFieldUpdate&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/nanoFramework.Runtime.InFieldUpdate.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.Runtime.InFieldUpdate/) |

## Update sessions

An update image is staged into the secondary slot through an **update session**. Opening one
claims the image for this writer, so a managed download, a host writing over the Wire Protocol
and native code can never interleave chunks into the same slot.

```csharp
// total length is the size of the signed package (content length of the download)
UpdateSession session = UpdateManager.StartUpdateSession(ImageType.Deployment, totalLength);

if (session == null)
{
    // Busy, SwapInFlight, TooLarge, FlashError - see UpdateSessionResult
    Debug.WriteLine($"could not start: {UpdateManager.GetLastSessionError()}");
    return;
}

// chunks are appended in order; offset/count index into the buffer, as for Stream.Write
while (!session.IsComplete)
{
    int read = ReadNextChunk(buffer);

    if (!UpdateManager.StoreImageChunk(session, buffer, 0, read))
    {
        // the position did not advance, so the chunk can be retried
        break;
    }
}

if (UpdateManager.CompleteUpdateSession(session) == UpdateSessionResult.Success)
{
    // the image is verified (structure + SHA-256) and marked for a test swap
    UpdateManager.RequestReboot();
}
```

After the reboot MCUboot swaps the image in. The new application validates itself and calls
`ConfirmDeploymentImage()`; without that confirmation the previous image is restored on the
next reboot.

### Resuming an interrupted download

A download interrupted by a reboot, a lost connection or a watchdog does not have to start over.
Nothing is persisted for this: the secondary slot itself carries the state, and
`ResumeUpdateSession` works out how far the previous session got.

```csharp
// the first 32 bytes are the MCUboot header - cheap to fetch and enough to tell this image
// from whatever else might be sitting in the slot
byte[] expectedHeader = HttpRange(url, 0, 31);

UpdateSession session =
    UpdateManager.ResumeUpdateSession(ImageType.Deployment, totalLength, expectedHeader)
    ?? UpdateManager.StartUpdateSession(ImageType.Deployment, totalLength);

while (session != null && !session.IsComplete)
{
    // ask the server only for what is missing
    byte[] chunk = HttpRange(url, session.NextOffset, session.NextOffset + ChunkSize - 1);

    if (!UpdateManager.StoreImageChunk(session, chunk, 0, chunk.Length))
    {
        // pause instead of discarding: the next run resumes from here
        UpdateManager.AbortUpdateSession(session, false);
        return;
    }
}
```

`session.NextOffset` is where the server should continue from. It is never further than what was
actually stored: the runtime rewinds to the start of the erase block holding the last written
byte and erases it, because a block interrupted mid-program cannot be repaired by writing to it
again.

Passing `expectedHeader` matters. After a completed swap the secondary slot holds the
*previous* image, with a perfectly valid header - only the caller can tell that apart from a
paused download. Without the check, `ResumeUpdateSession` would happily resume the wrong image
(`CompleteUpdateSession` would then fail with `HashMismatch`, but only after the whole transfer).

### Session rules

- One session per image, across every writer on the device. `GetUpdateSessionOwner(image)` says
  who holds it; a second writer gets `UpdateSessionResult.Busy`.
- Reopening as the same writer replaces its own session, so an abandoned handle cannot wedge the
  image for good.
- `StartUpdateSession` erases the whole secondary slot, which takes a few seconds on large
  external flash. Feed the watchdog before calling it.
- `EraseSecondaryImage` is refused while a session is open.
- After `CompleteUpdateSession` or `AbortUpdateSession` the handle is stale: further use returns
  `UpdateSessionResult.BadToken`.
- `AbortUpdateSession(session, eraseSlot: false)` is a pause - what was stored stays for a later
  `ResumeUpdateSession`. With `eraseSlot: true` the session is released even if the erase fails,
  so there is nothing to retry; the return value tells you whether the slot is actually clean.
- Sessions live in RAM and every reboot releases them, a CLR-only restart (a deploy from Visual
  Studio, `RequestReboot`) included. A device can therefore never come back with a slot claimed by
  an application that is gone. What was already written to the slot is untouched, which is exactly
  what `ResumeUpdateSession` picks up.
- `GetLastSessionError()` reports the last outcome the runtime recorded for *any* writer, so read
  it right after the call you are interested in.

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

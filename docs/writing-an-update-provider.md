# Writing an update provider

This guide is for developers building an update library or agent on top of
`nanoFramework.Runtime.InFieldUpdate` - code that finds out whether a new image is available from
some provider (an HTTP server, a cloud device-management service, MQTT, a file on an SD card...),
fetches it and stages it on the device.

It assumes you have read the [overview](overview.md) and [Update sessions](update-sessions.md).

## Packages

| Package | Contains | Who references it |
|:-|:-|:-|
| `nanoFramework.Runtime.InFieldUpdate` | `UpdateManager`, `UpdateSession`, confirm/revert. | Every application that updates. |
| `nanoFramework.Runtime.InFieldUpdate.Provider` | `IUpdateProvider`, `UpdateAgentOptions`, `HealthCheck`. | Update libraries and agents. |

The provider package is a contract, not an agent: it fixes the shape of a provider and of the
settings an agent takes from the application, so that providers and agents from different
libraries fit together. The agent itself - the decision logic below - is yours to write.

## Who does what

| Your provider library | `UpdateManager` |
|:-|:-|
| Knows where updates come from and how to talk to that source. | Knows nothing about the source. |
| Discovers the offered version, total length and header bytes. | Checks the image fits the slot. |
| Transfers bytes, handles connection loss, retries, back-off. | Claims the slot, erases it, appends chunks strictly in order. |
| Decides *when* to update and *when* to reboot. | Verifies structure and SHA-256, schedules the test swap. |
| Makes sure the new image confirms itself (or does it on its behalf). | Swaps and reverts via MCUboot. |

The package the provider serves must be the **signed MCUboot image, byte for byte**: header,
payload and TLV area, as produced by the build. `totalLength` is its exact size.

## The provider contract

```csharp
public interface IUpdateProvider
{
    // false when nothing is on offer (or the provider cannot be reached)
    bool TryGetOffer(out Version version, out int totalLength);

    // reads up to buffer.Length bytes starting at 'offset';
    // returns the number of bytes read, 0 at the end of the image, -1 on transport error
    int Read(int offset, byte[] buffer);
}
```

Between them, the two members give the agent the four things it needs:

1. The **version** on offer (to decide whether to update).
2. The **total length** of the signed image.
3. The **first 32 bytes** (the MCUboot header) - `Read(0, new byte[32])` - to recognise a paused
   download of the same image.
4. The **bytes from a given offset** onwards, so a download can resume where it stopped.

Over HTTP these map to a manifest (or a `HEAD` request with `Content-Length`), a
`Range: bytes=0-31` request and `Range: bytes=<offset>-<end>` requests. If the source cannot
serve ranges, `Read` can still honour any offset by reading and discarding the bytes before it -
wasteful, but correct. A `Read` may return fewer bytes than asked; the agent stores what it got and
asks again from the new position.

## Agent options

`UpdateAgentOptions` carries what the application decides, not the provider:

| Property | Default | Meaning |
|:-|:-|:-|
| `ChunkSize` | 4096 | Size of the buffer the image streams through - each `Read` and each `StoreImageChunk`. Also the RAM a download needs. |
| `MaxRetries` | 3 | How many times to download again after the staged image fails verification, before giving up on the offer. 0 means a single attempt. |
| `ConfirmHook` | `null` | `HealthCheck` the agent runs while the deployment image is on trial (`Testing`): `true` confirms it, `false` reverts it. `null` leaves confirmation to the application. |

## The flow step by step

### 1. On boot: settle the previous update

Before anything else, deal with an image that was just swapped in. If
`GetStatus(ImageType.Deployment)` is `Testing`, run the health check and confirm or revert - see
[Confirm only after a health check](confirm-and-revert.md#confirm-only-after-a-health-check). An
agent must never start a new download while the running image is unconfirmed: if it is rolled back,
the work is wasted, and a new session would fail anyway while a swap is pending.

If your library ships as a NuGet package used by other applications, decide whether confirmation is
the application's job (usually: it knows what "healthy" means) or the library's. `ConfirmHook` is
how the application hands the library that decision: when it is set, the agent calls it and
confirms or reverts; when it is `null`, the agent waits for the application to confirm.

### 2. Check whether an update is available

Ask the provider what it has and compare it with what is running:

```csharp
if (!provider.TryGetOffer(out Version offered, out int totalLength))
{
    return;
}

ImageInfo running = UpdateManager.GetPrimaryImageInfo(ImageType.Deployment);
```

See [Is an update needed?](querying-images.md#is-an-update-needed). Compare against the version in
the image header rather than, say, an assembly version, because that is what `ImageInfo.Version`
reports after the swap.

### 3. Check the device is ready

```csharp
if (UpdateManager.GetStatus(image) != UpdateStatus.Confirmed)
{
    // Testing: settle it first. *Pending: a swap is already scheduled - wait for the reboot.
    return;
}

UpdateSessionOwner owner = UpdateManager.GetUpdateSessionOwner(image);

if (owner != UpdateSessionOwner.None && owner != UpdateSessionOwner.Managed)
{
    // a host (WireProtocol) or native agent is writing right now - try again later
    return;
}
```

A session owned by `Managed` can be taken over: reopening as the same writer replaces the previous
session.

### 4. Fetch the header

Read the first 32 bytes of the image from the provider. This is cheap and lets the agent resume
safely.

### 5. Resume first, start fresh otherwise

```csharp
UpdateSessionResult result = UpdateManager.ResumeUpdateSession(image, totalLength, header, out UpdateSession session);

if (result == UpdateSessionResult.NoImage || result == UpdateSessionResult.HeaderMismatch)
{
    // nothing useful in the slot: start over (erases the whole slot)
    result = UpdateManager.StartUpdateSession(image, totalLength, out session);
}

if (result != UpdateSessionResult.Success)
{
    // Busy, SwapInFlight, TooLarge, NoSlot, FlashError: not something a fresh start will fix
    return;
}
```

Always pass the header to `ResumeUpdateSession`. After a successful swap the secondary slot holds
the *previous* image with a valid header; without the check the agent would "resume" into it and
only find out at the end, with a `HashMismatch`.

### 6. Download the rest

Read from `session.NextOffset` and append until `session.IsComplete`:

- If a chunk is rejected, the position did not advance - the same chunk can be retried. The result
  says why: `BadToken` means the session is gone (resume again); `TooLarge` or `BadMagic` mean the
  package is wrong.
- If the transport fails, **pause** with `AbortUpdateSession(session, false)`. The next attempt,
  even after a reboot, resumes from the slot.
- Do not keep the whole image in RAM: stream it through one buffer of `ChunkSize` bytes.
- `StartUpdateSession` erases the whole slot and can take a few seconds on large external flash;
  allow for that in any timeouts of your own.

### 7. Complete, then reboot

```csharp
UpdateSessionResult result = UpdateManager.CompleteUpdateSession(session);
```

- `Success` - image verified and scheduled for a test swap. Reboot now, or record that a reboot is
  needed and do it at a convenient time.
- `Incomplete` - the session is still open; keep downloading.
- `HashMismatch`, `BadTlv`, `BadMagic` - the data is corrupt or not the image the provider
  advertised. The session is closed. Download again from scratch (`StartUpdateSession`, not
  resume), at most `MaxRetries` more times - a provider serving a broken package must not make the
  device loop forever. When giving up, erase the slot so the next run does not resume the bad image.

After the reboot the new image starts in `Testing` and step 1 applies.

## Skeleton

The following is an **example**, not part of the library: it is the complete decision logic
described above, on the shipped contract. The same agent lives in the test project as
[`ReferenceAgent`](../Tests/InFieldUpdateTests/Helpers/ReferenceAgent.cs), where it is run against
a real device.

```csharp
using System;
using nanoFramework.Runtime.InFieldUpdate;

public class UpdateAgent
{
    private const int HeaderSize = 32;

    private readonly IUpdateProvider _provider;
    private readonly ImageType _image;
    private readonly UpdateAgentOptions _options;

    public UpdateAgent(IUpdateProvider provider, ImageType image, UpdateAgentOptions options)
    {
        _provider = provider;
        _image = image;
        _options = options ?? new UpdateAgentOptions();
    }

    /// <returns><see langword="true"/> when an image is staged and a reboot will apply it.</returns>
    public bool Run()
    {
        if (!SettleRunningImage())
        {
            return false;
        }

        if (!_provider.TryGetOffer(out Version offered, out int totalLength))
        {
            return false;
        }

        ImageInfo running = UpdateManager.GetPrimaryImageInfo(_image);

        if (running != null && !IsNewer(offered, running.Version))
        {
            return false;
        }

        byte[] header = new byte[HeaderSize];

        if (_provider.Read(0, header) != HeaderSize)
        {
            return false;
        }

        byte[] buffer = new byte[_options.ChunkSize];

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            // resume only on the first attempt: after a verification failure the slot holds the
            // bad image, which must be erased rather than continued
            UpdateSession session;
            UpdateSessionResult result = attempt == 0
                ? OpenSession(totalLength, header, out session)
                : UpdateManager.StartUpdateSession(_image, totalLength, out session);

            if (result != UpdateSessionResult.Success)
            {
                // Busy, SwapInFlight, TooLarge, NoSlot, FlashError
                return false;
            }

            if (!Download(session, buffer))
            {
                // pause: the next Run() - even after a reboot - continues from here
                UpdateManager.AbortUpdateSession(session, false);
                return false;
            }

            if (UpdateManager.CompleteUpdateSession(session) == UpdateSessionResult.Success)
            {
                return true;
            }

            // HashMismatch / BadTlv / BadMagic: not the advertised image - download again
        }

        // give up, and make sure the next Run() does not resume the bad image
        UpdateManager.EraseSecondaryImage(_image);
        return false;
    }

    private bool SettleRunningImage()
    {
        UpdateStatus status = UpdateManager.GetStatus(_image);

        if (status == UpdateStatus.Confirmed)
        {
            return true;
        }

        if (status != UpdateStatus.Testing || _options.ConfirmHook == null)
        {
            // a swap is pending, or confirming is the application's job
            return false;
        }

        if (_options.ConfirmHook())
        {
            return UpdateManager.ConfirmDeploymentImage();
        }

        UpdateManager.RequestDeploymentRevert();
        return false;
    }

    private UpdateSessionResult OpenSession(int totalLength, byte[] header, out UpdateSession session)
    {
        UpdateSessionResult result = UpdateManager.ResumeUpdateSession(_image, totalLength, header, out session);

        if (result != UpdateSessionResult.NoImage && result != UpdateSessionResult.HeaderMismatch)
        {
            // resumed, or a failure a fresh start would not fix
            return result;
        }

        // erases the whole secondary slot, which can take a few seconds
        return UpdateManager.StartUpdateSession(_image, totalLength, out session);
    }

    private bool Download(UpdateSession session, byte[] buffer)
    {
        while (!session.IsComplete)
        {
            int read = _provider.Read(session.NextOffset, buffer);

            if (read <= 0)
            {
                return false;
            }

            if (UpdateManager.StoreImageChunk(session, buffer, 0, read) != UpdateSessionResult.Success)
            {
                // position did not advance; a caller could retry - here we pause
                return false;
            }
        }

        return true;
    }

    private static bool IsNewer(Version a, Version b)
    {
        if (a.Major != b.Major) return a.Major > b.Major;
        if (a.Minor != b.Minor) return a.Minor > b.Minor;
        if (a.Build != b.Build) return a.Build > b.Build;
        return a.Revision > b.Revision;
    }
}
```

Typical use:

```csharp
UpdateAgentOptions options = new UpdateAgentOptions
{
    ChunkSize = 2048,
    MaxRetries = 2,
    ConfirmHook = () => SensorsRespond() && CanReach(manifestUrl),
};

if (new UpdateAgent(new MyHttpProvider(manifestUrl), ImageType.Deployment, options).Run())
{
    UpdateManager.RequestReboot();
}
```

## Testing an agent

Test the agent against the real `UpdateManager` on a device, with a fake provider in place of the
transport. An `IUpdateProvider` that serves an image built in RAM and can inject faults (drop the
connection at a given offset, flip a byte, offer nothing) covers resume, retry cap and
header-mismatch paths without a server. The test project's
[`FakeUpdateProvider`](../Tests/InFieldUpdateTests/Helpers/FakeUpdateProvider.cs) and
[`ProviderFlowTests`](../Tests/InFieldUpdateTests/ProviderFlowTests.cs) show how; the images come
from [`McuBootImageBuilder`](../Tests/InFieldUpdateTests/Helpers/McuBootImageBuilder.cs), which
builds unsigned images that pass `CompleteUpdateSession` verification.

The IFU native code only exists on MCUboot-enabled targets, so these tests need hardware - the
Windows nanoCLR cannot run them.

## Pitfalls checklist

- **Always pass `expectedHeader` to `ResumeUpdateSession`.** Otherwise the previous image left in
  the slot after a swap looks like a paused download.
- **Session handles do not survive a reboot or a CLR restart.** Keep nothing but what you can
  recompute (URL, version, length) and resume from the slot.
- **Act on the result of each call.** Every session operation returns its own
  `UpdateSessionResult`; the `out` session is `null` unless the result is `Success`.
- **Serve the signed image unchanged.** Any transformation breaks the SHA-256 check at
  `CompleteUpdateSession` or the signature check at boot.
- **Check the status before downloading.** A swap already pending (`SwapInFlight`) or an
  unconfirmed image (`Testing`) means the download should wait.
- **Make sure the new image can confirm itself** - and can still reach your provider. An image
  that never confirms is rolled back on every reboot; an image that confirms but cannot update is
  stuck in the field.
- **Cap retries** after `HashMismatch`/`BadTlv` with `MaxRetries`, and start retries fresh rather
  than resuming, so a broken package on the server does not keep the device downloading forever.
- **Be careful with `ImageType.NanoClr`.** Test the whole flow with deployment images first; the
  nanoCLR is confirmed at startup and has no automatic test-and-revert cycle (see
  [nanoCLR image](confirm-and-revert.md#nanoclr-image)).

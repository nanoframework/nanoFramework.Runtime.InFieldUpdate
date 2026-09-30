# Writing an update provider

This guide is for developers building an update library or agent on top of
`nanoFramework.Runtime.InFieldUpdate` - code that finds out whether a new image is available from
some provider (an HTTP server, a cloud device-management service, MQTT, a file on an SD card...),
fetches it and stages it on the device.

It assumes you have read the [overview](overview.md) and [Update sessions](update-sessions.md).

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

## What the provider needs to expose

Whatever the transport, the agent needs four things from it:

1. The **version** on offer (to decide whether to update).
2. The **total length** of the signed image.
3. The **first 32 bytes** (the MCUboot header), to recognise a paused download of the same image.
4. The **bytes from a given offset** onwards, so a download can resume where it stopped.

Over HTTP these map to a manifest (or a `HEAD` request with `Content-Length`), a
`Range: bytes=0-31` request and `Range: bytes=<NextOffset>-<end>` requests. If the source cannot
serve ranges, resuming is still possible by reading and discarding the first `NextOffset` bytes of
the stream - wasteful, but correct.

## The flow step by step

### 1. On boot: settle the previous update

Before anything else, deal with an image that was just swapped in. If
`GetStatus(ImageType.Deployment)` is `Testing`, run the health check and confirm or revert - see
[Confirm only after a health check](confirm-and-revert.md#confirm-only-after-a-health-check). An
agent must never start a new download while the running image is unconfirmed: if it is rolled back,
the work is wasted, and a new session would fail anyway while a swap is pending.

If your library ships as a NuGet package used by other applications, decide whether confirmation is
the application's job (usually: it knows what "healthy" means) or the library's (offer a hook, such
as a self-test callback).

### 2. Check whether an update is available

Ask the provider what it has and compare it with what is running:

```csharp
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

if (UpdateManager.GetUpdateSessionOwner(image) != UpdateSessionOwner.None
    && UpdateManager.GetUpdateSessionOwner(image) != UpdateSessionOwner.Managed)
{
    // a host (WireProtocol) or native agent is writing right now - try again later
    return;
}
```

A session owned by `Managed` can be taken over: reopening as the same writer replaces the previous
session.

### 4. Fetch the length and header

Get `totalLength` and the first 32 bytes of the image from the provider. These are cheap and let
the agent resume safely.

### 5. Resume first, start fresh otherwise

```csharp
UpdateSession session = UpdateManager.ResumeUpdateSession(image, totalLength, header);

if (session == null)
{
    UpdateSessionResult why = UpdateManager.GetLastSessionError();

    if (why != UpdateSessionResult.NoImage && why != UpdateSessionResult.HeaderMismatch)
    {
        // Busy, SwapInFlight, TooLarge, FlashError: not something a fresh start will fix
        return;
    }

    // nothing useful in the slot: start over (erases the whole slot)
    session = UpdateManager.StartUpdateSession(image, totalLength);
}
```

Always pass the header to `ResumeUpdateSession`. After a successful swap the secondary slot holds
the *previous* image with a valid header; without the check the agent would "resume" into it and
only find out at the end, with a `HashMismatch`.

### 6. Download the rest

Fetch from `session.NextOffset` and append until `session.IsComplete`:

- If a chunk is rejected, the position did not advance - the same chunk can be retried. Check
  `GetLastSessionError()`: `BadToken` means the session is gone (resume again); `TooLarge` or
  `BadMagic` mean the package is wrong.
- If the transport fails, **pause** with `AbortUpdateSession(session, false)`. The next attempt,
  even after a reboot, resumes from the slot.
- Do not keep the whole image in RAM: stream it through a fixed-size buffer.
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
  advertised. The session is closed. Download again from scratch (`StartUpdateSession`), and give
  up after a few attempts - a provider serving a broken package should not make the device loop
  forever.

After the reboot the new image starts in `Testing` and step 1 applies.

## Skeleton

The following is an **example**, not part of the library. `IUpdateSource` stands for whatever
transport your provider uses; the rest is the complete decision logic described above.

```csharp
using System;
using nanoFramework.Runtime.InFieldUpdate;

// example abstraction over the provider - not part of nanoFramework.Runtime.InFieldUpdate
public interface IUpdateSource
{
    // returns false if the provider has nothing on offer
    bool TryGetOffer(out Version version, out int totalLength);

    // reads up to buffer.Length bytes of the image starting at 'offset';
    // returns the number of bytes read, or -1 on transport error
    int Read(int offset, byte[] buffer);
}

public class UpdateAgent
{
    private const int ChunkSize = 4096;
    private const int HeaderSize = 32;

    private readonly IUpdateSource _source;
    private readonly ImageType _image;

    public UpdateAgent(IUpdateSource source, ImageType image)
    {
        _source = source;
        _image = image;
    }

    /// <returns><see langword="true"/> when an image is staged and a reboot will apply it.</returns>
    public bool Run()
    {
        if (UpdateManager.GetStatus(_image) != UpdateStatus.Confirmed)
        {
            // Testing: confirm/revert first; *Pending: a swap is already scheduled
            return false;
        }

        if (!_source.TryGetOffer(out Version offered, out int totalLength))
        {
            return false;
        }

        ImageInfo running = UpdateManager.GetPrimaryImageInfo(_image);

        if (running != null && !IsNewer(offered, running.Version))
        {
            return false;
        }

        byte[] header = new byte[HeaderSize];

        if (_source.Read(0, header) != HeaderSize)
        {
            return false;
        }

        UpdateSession session = OpenSession(totalLength, header);

        if (session == null)
        {
            return false;
        }

        if (!Download(session))
        {
            // pause: the next Run() - even after a reboot - continues from here
            UpdateManager.AbortUpdateSession(session, false);
            return false;
        }

        UpdateSessionResult result = UpdateManager.CompleteUpdateSession(session);

        if (result != UpdateSessionResult.Success)
        {
            // HashMismatch / BadTlv / BadMagic: the staged data is not the advertised image.
            // Discard it so the next Run() starts from scratch rather than resuming it.
            UpdateManager.EraseSecondaryImage(_image);
            return false;
        }

        return true;
    }

    private UpdateSession OpenSession(int totalLength, byte[] header)
    {
        UpdateSession session = UpdateManager.ResumeUpdateSession(_image, totalLength, header);

        if (session != null)
        {
            return session;
        }

        UpdateSessionResult why = UpdateManager.GetLastSessionError();

        if (why != UpdateSessionResult.NoImage && why != UpdateSessionResult.HeaderMismatch)
        {
            // Busy, SwapInFlight, TooLarge, FlashError
            return null;
        }

        // erases the whole secondary slot, which can take a few seconds
        return UpdateManager.StartUpdateSession(_image, totalLength);
    }

    private bool Download(UpdateSession session)
    {
        byte[] buffer = new byte[ChunkSize];

        while (!session.IsComplete)
        {
            int read = _source.Read(session.NextOffset, buffer);

            if (read <= 0)
            {
                return false;
            }

            if (!UpdateManager.StoreImageChunk(session, buffer, 0, read))
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
if (new UpdateAgent(new MyHttpSource(manifestUrl), ImageType.Deployment).Run())
{
    UpdateManager.RequestReboot();
}
```

## Pitfalls checklist

- **Always pass `expectedHeader` to `ResumeUpdateSession`.** Otherwise the previous image left in
  the slot after a swap looks like a paused download.
- **Session handles do not survive a reboot or a CLR restart.** Keep nothing but what you can
  recompute (URL, version, length) and resume from the slot.
- **Read `GetLastSessionError()` immediately** after the failing call. It is one value for the
  whole device and other writers update it too.
- **Serve the signed image unchanged.** Any transformation breaks the SHA-256 check at
  `CompleteUpdateSession` or the signature check at boot.
- **Check the status before downloading.** A swap already pending (`SwapInFlight`) or an
  unconfirmed image (`Testing`) means the download should wait.
- **Make sure the new image can confirm itself** - and can still reach your provider. An image
  that never confirms is rolled back on every reboot; an image that confirms but cannot update is
  stuck in the field.
- **Cap retries** after `HashMismatch`/`BadTlv`, so a broken package on the server does not keep
  the device downloading forever.
- **Be careful with `ImageType.NanoClr`.** Test the whole flow with deployment images first; the
  nanoCLR is confirmed at startup and has no automatic test-and-revert cycle (see
  [nanoCLR image](confirm-and-revert.md#nanoclr-image)).

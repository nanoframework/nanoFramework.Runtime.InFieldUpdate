# Updating the nanoCLR and the deployment together

Most updates replace a single image: a new deployment on the same firmware, or new firmware that
still runs the same deployment. Each is staged with an [update session](update-sessions.md),
completed, and swapped in on its own.

Some updates can't be split like that. A new nanoCLR may change the native side of a class library
(its native contract), and the deployment built against it only loads on that nanoCLR - and the old
deployment no longer loads on the new nanoCLR. Such a pair has to land **together**, and if
anything goes wrong it has to roll back **together**. This page explains how to do that from the
device itself, for example in an update agent that downloads both images over the network, and
what the device guarantees once you have.

> Updating from an SD card or a USB stick already works this way: an update bundle
> (`nanoUpdate-*.bin`, built with `nanoff pack update`) carries both images and the bootloader
> imports them as a pair. This page is about staging the pair at run time with `UpdateManager`.

## Why two separate updates are not enough

`CompleteUpdateSession(session)` marks an image pending as soon as it verifies. Completing the
nanoCLR and then the deployment one after the other leaves a window in which only one of them is
pending, and any reboot in that window swaps it in alone: a power cut, a watchdog, a crash, or an
agent that reboots too early.

| Pending alone when the device reboots | Result |
|:-|:-|
| nanoCLR | The new nanoCLR comes up with the old deployment, which no longer loads. The old deployment is confirmed, so nothing reverts it: the device is stuck until it is updated again. |
| Deployment | The old nanoCLR can't load the new deployment. Because the deployment is unconfirmed, the device resets and MCUboot reverts it: the device is back on its previous pair. |

The same window opens while the second image is still downloading, which on a slow link can take
minutes.

## The flow

Stage both images, complete each **without** requesting the update, then request both in one call:

```csharp
// 1. Stage both images. CompleteUpdateSession with requestUpdate: false only verifies the image.
UpdateSessionResult result = Stage(ImageType.NanoClr, clrProvider);

if (result == UpdateSessionResult.Success)
{
    result = Stage(ImageType.Deployment, deploymentProvider);
}

// 2. Request both, all or nothing.
if (result == UpdateSessionResult.Success)
{
    result = UpdateManager.RequestUpdate(ImageType.NanoClr, ImageType.Deployment);
}

// 3. Swap them in.
if (result == UpdateSessionResult.Success)
{
    UpdateManager.RequestReboot();
}

static UpdateSessionResult Stage(ImageType image, IUpdateProvider provider)
{
    // open (or resume) a session and store the image as usual - see "Update sessions"
    UpdateSession session = OpenAndDownload(image, provider);

    return UpdateManager.CompleteUpdateSession(session, requestUpdate: false);
}
```

`OpenAndDownload` stands for the usual staging code: `StartUpdateSession` or `ResumeUpdateSession`,
then `StoreImageChunk` until the image is complete. See [Update sessions](update-sessions.md) and
[Writing an update provider](writing-an-update-provider.md).

The order in which the two images are downloaded does not matter: nothing is pending until
`RequestUpdate` succeeds.

### `CompleteUpdateSession(session, requestUpdate: false)`

Verifies the staged image exactly like `CompleteUpdateSession(session)` (structure plus SHA-256)
and closes the session, but does not mark the image pending. The image waits, verified, for
`RequestUpdate`. The results are the same as for the one-argument overload, and
`GetStatus(image)` stays `Confirmed`.

The waiting state lives in RAM. It is dropped by:

- a reboot, or a CLR restart (for example a deployment from Visual Studio);
- opening a session on the image (`StartUpdateSession` or `ResumeUpdateSession`);
- erasing its secondary slot (`EraseSecondaryImage`).

The image itself stays in the secondary slot. After a reboot, `ResumeUpdateSession` with the same
header finds it complete (`NextOffset == TotalLength`), so complete it again with
`requestUpdate: false` - nothing is downloaded twice.

### `RequestUpdate`

```csharp
UpdateSessionResult result = UpdateManager.RequestUpdate(ImageType.NanoClr, ImageType.Deployment);
```

Marks every listed image pending for a test swap on the next reboot, or none of them:

1. Every image must be waiting, completed with `requestUpdate: false`. This is deliberate: after a
   swap the secondary slot holds the *previous* image, which verifies just as well, and requesting
   it would put the old image back.
2. Every image is verified again.
3. All of them are marked pending - the deployment first. If power is lost between the two, only
   the deployment is pending, which is the case that rolls back on its own (see the table above).
4. If marking one of them fails, the secondary slots already marked are erased, so nothing is left
   pending. Both images have to be downloaded again.

| Result | Meaning | What to do |
|:-|:-|:-|
| `Success` | Every image is pending (`TestPending`). | `RequestReboot` now or later. |
| `NoImage` | An image is not waiting: not completed with `requestUpdate: false`, already requested, or dropped since (reboot, new session, erase). | Resume and complete it again with `requestUpdate: false`. |
| `Busy` | A session or an erase is under way on one of the images. | Retry once it is done. |
| `SwapInFlight` | One of the images already has a swap pending (requested by some other call). The images keep waiting. | Reboot to let that swap through, then stage the images again; or erase that image's secondary slot and stage it again. |
| `BadMagic`, `BadTlv`, `HashMismatch` | An image no longer verifies. | Download it again. |
| `FlashError` | Marking an image pending failed; the slots already marked were erased. | Download the erased images again. |
| `BadArgument` | Empty list, unknown image, or an image listed twice. | Fix the call. |

`RequestUpdate` works for a single image too, but it is only useful for images that must land
together.

## After the reboot: kept or reverted as a pair

When the nanoCLR and the deployment were swapped in by the same reboot, the device keeps or reverts
them as a pair:

- **The nanoCLR waits for the deployment.** Normally the nanoCLR confirms itself as soon as it
  starts. After a pair update it does not: `GetStatus(ImageType.NanoClr)` reports `Testing` until
  the deployment is confirmed.
- **`ConfirmDeploymentImage()` confirms both.** It confirms the deployment and then the nanoCLR,
  and returns `true` only when both are confirmed. Confirm only once the application has checked
  that it works - see [Confirm only after a health check](confirm-and-revert.md#confirm-only-after-a-health-check).
- **Anything else reverts both.** A deployment that fails to load, has no entry point, is not
  confirmed before its [confirmation timeout](confirm-and-revert.md#confirmation-timeout), or is
  still unconfirmed at the next reboot - for whatever reason - is reverted together with the
  nanoCLR it came with. `RequestDeploymentRevert()` followed by a reboot does the same.

The bootloader also takes care of a power cut *during* the swap. It swaps the nanoCLR first and
the deployment second, so an interrupted swap never leaves the new nanoCLR with the old deployment:
the nanoCLR, still unconfirmed, is reverted, and a deployment the old nanoCLR can't load is
reverted after it.

### What counts as "together"

The device decides from the state of the two images at power-on: the pair rule applies whenever
**both** the nanoCLR and the deployment were test-swapped by the same reboot. It does not matter
how they were staged - `RequestUpdate`, two `CompleteUpdateSession(session)` calls that happened
to finish before the same reboot, a host over the Wire Protocol, or an update bundle on an SD card
or USB stick. `RequestUpdate` is what guarantees that they reach that reboot together.

It does not apply when:

- **only the nanoCLR is updated** (the deployment is already confirmed): the nanoCLR confirms
  itself at startup as before;
- **only the deployment is updated**: the usual [deployment test cycle](confirm-and-revert.md#the-deployment-test-cycle);
- **the deployment has no MCUboot trailer** (for example one written by the debugger during
  development): there is no deployment to wait for, so the nanoCLR confirms itself;
- **the two images were swapped by different reboots**: they are two separate updates, each with
  its own test cycle.

## Debugging a pair update

While the deployment is unconfirmed, so is the nanoCLR. A debug session cancels the confirmation
timeout, but the images stay unconfirmed: if the device resets before the application calls
`ConfirmDeploymentImage()`, both images are reverted. Confirm from the application (or the
debugger's image confirm command) once you are done.

## Requirements

`CompleteUpdateSession(session, bool)` and `RequestUpdate` are implemented by the firmware. The
library only loads on firmware built with the matching native version of
`nanoFramework.Runtime.InFieldUpdate`; firmware that predates these methods rejects the newer
library.

# Confirm, revert and reboot

Staging an image only schedules a swap. What happens next - the reboot, the test run and the
decision to keep or reject the new image - is driven by the methods described here.

## `RequestReboot`

```csharp
UpdateManager.RequestReboot();
```

Reboots the device. Any swap that has been scheduled (test, permanent or revert) is applied by
MCUboot during the reboot. Every open update session is released.

Call it right after a successful `CompleteUpdateSession`, or defer it to a convenient moment (end
of a work cycle, maintenance window, user acknowledgement). The pending swap survives until the next
reboot, whatever causes it.

## The deployment test cycle

A completed update session marks the new deployment image for a **test** swap:

1. On reboot MCUboot swaps the new image into the primary slot; `GetStatus(ImageType.Deployment)`
   returns `UpdateStatus.Testing`.
2. The new application runs and checks that it actually works.
3. It then either
   - calls `ConfirmDeploymentImage()` - the image becomes permanent and the status returns to
     `Confirmed`; or
   - does nothing (crashes, hangs, never gets that far) or calls `RequestDeploymentRevert()`
     - on the next reboot the previous image is restored.

This makes a bad update self-healing: an image that cannot get far enough to confirm itself is
rolled back automatically.

### `ConfirmDeploymentImage`

```csharp
bool confirmed = UpdateManager.ConfirmDeploymentImage();
```

Makes the running deployment image permanent. If it is not called while the deployment is in the
`Testing` state, the previous image is restored on the next reboot. Returns `true` if the image
was confirmed.

### `RequestDeploymentRevert`

```csharp
bool scheduled = UpdateManager.RequestDeploymentRevert();
UpdateManager.RequestReboot();
```

Explicitly rejects the new image: the previous deployment image is restored on the next reboot.
Valid only while the deployment image is running in the unconfirmed `Testing` state. Returns `true`
if the revert was scheduled.

Not confirming has the same end result, but an explicit revert followed by a reboot rolls back
straight away instead of waiting for whatever triggers the next reboot.

### Confirm only after a health check

The simplest application confirms unconditionally as the first thing in `Main` - the
[IFU-blink-app sample](../Samples/IFU-blink-app/Program.cs) does that. It proves the image boots,
but not that it works. A more robust pattern checks what matters to the device before confirming:

```csharp
public static void Main()
{
    if (UpdateManager.GetStatus(ImageType.Deployment) == UpdateStatus.Testing)
    {
        if (SelfTest())
        {
            // peripherals initialised, network reachable, update server reachable...
            UpdateManager.ConfirmDeploymentImage();
        }
        else
        {
            UpdateManager.RequestDeploymentRevert();
            UpdateManager.RequestReboot();
        }
    }

    // normal application start
}
```

Things worth checking in `SelfTest`:

- The peripherals and configuration the application depends on initialise.
- The device can still reach the update provider - otherwise a bad image could never be replaced
  by a fixed one.
- Anything else that would leave the device useless in the field.

Keep it bounded in time: until the image is confirmed, any reset brings the previous image back,
so a self-test that never completes means the update does not stick.

## nanoCLR image

The nanoCLR image behaves differently: **the running nanoCLR is already confirmed at startup**, so
there is no test cycle to confirm and `RequestDeploymentRevert` does not apply to it.

### `RequestClrRevert`

```csharp
bool scheduled = UpdateManager.RequestClrRevert();
UpdateManager.RequestReboot();
```

Schedules a swap of the image currently in the nanoCLR **secondary** slot on the next reboot. It
cannot "un-confirm" the running firmware; it can only swap in a valid image that is already present
there - for example a copy of the previous firmware kept in that slot. Returns `false` if the
nanoCLR secondary slot does not hold a valid image to revert to. A reboot is required to complete
the swap.

Check what is there first with `GetSecondaryImageInfo(ImageType.NanoClr)` (see
[Querying images](querying-images.md)).

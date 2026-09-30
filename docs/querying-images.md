# Querying images

Before deciding whether to download an update - and after staging one - an application usually
wants to know what is on the device: which version is running, whether it is confirmed, whether
something is already staged or scheduled. The query methods of `UpdateManager` answer that. They
are read-only and cheap.

## `GetStatus`

```csharp
UpdateStatus status = UpdateManager.GetStatus(ImageType.Deployment);
```

Returns the lifecycle state of an image - see the [state table](overview.md#update-states). The
states that matter most in practice:

- `Confirmed` - nothing in flight. The only state in which it makes sense to start staging a new
  image.
- `Testing` - the running image was just swapped in and is waiting for confirmation. Confirm or
  revert it first (see [Confirm and revert](confirm-and-revert.md)).
- `TestPending`, `PermanentPending`, `RollbackPending` - a swap is already scheduled for the next
  reboot. Opening an update session now fails with `UpdateSessionResult.SwapInFlight`; stage after
  the reboot.

## `GetPrimaryImageInfo` and `GetSecondaryImageInfo`

```csharp
ImageInfo running = UpdateManager.GetPrimaryImageInfo(ImageType.Deployment);
ImageInfo staged = UpdateManager.GetSecondaryImageInfo(ImageType.Deployment);
```

Return metadata about one slot of one image:

- `GetPrimaryImageInfo` - the primary (active) slot. `null` if no valid image is found.
- `GetSecondaryImageInfo` - the secondary (staging) slot. `null` if the slot is empty or invalid.

Always check for `null`: an erased secondary slot is the normal state on most devices.

## `GetImageList`

```csharp
ImageInfo[] images = UpdateManager.GetImageList();
```

Returns one entry per image/slot combination - both slots of every image - in a flat array. Each
entry identifies itself through `ImageInfo.Image` and `ImageInfo.Slot`, so filter on those rather
than relying on the array order.

## `ImageInfo`

| Property | Type | Meaning |
|:-|:-|:-|
| `Image` | `ImageType` | Which image this entry describes (nanoCLR or deployment). |
| `Slot` | `SlotId` | Which slot this entry describes (primary or secondary). |
| `Version` | `Version` | Version of the image, from its MCUboot header. |
| `ImageHash` | `byte[]` | SHA-256 of the image (32 bytes), or `null` if it could not be read. |
| `HasValidHeader` | `bool` | The image metadata was read successfully. **Structural check only** - the hash and signature are not verified. |
| `IsActive` | `bool` | This slot holds the currently running image. |
| `IsConfirmed` | `bool` | The image is confirmed (permanent). |
| `IsPending` | `bool` | A swap from this slot is pending on the next boot. |
| `IsBootable` | `bool` | The image is allowed to run. |
| `IsRollbackPending` | `bool` | A rollback affecting this image is scheduled for the next reboot. |

`ImageInfo` instances are created by the runtime only; there is no public constructor.

## Printing

`ImageInfo.ToString()` returns one `Header: value` line per property:

```csharp
Console.WriteLine(UpdateManager.GetPrimaryImageInfo(ImageType.Deployment));
```

```text
Image: Deployment
Slot: Primary
Version: 1.0.0.0
Valid: Yes
Active: Yes
Confirmed: Yes
Pending: No
Bootable: Yes
Rollback: No
```

The `ToTable()` extension method formats a whole list as an ASCII table - handy for logs and
diagnostics consoles:

```csharp
Console.WriteLine(UpdateManager.GetImageList().ToTable());
```

```text
+------------+-----------+---------+-------+--------+-----------+---------+----------+----------+
| Image      | Slot      | Version | Valid | Active | Confirmed | Pending | Bootable | Rollback |
+------------+-----------+---------+-------+--------+-----------+---------+----------+----------+
| nanoCLR    | Primary   | 1.12.0  | Yes   | Yes    | Yes       | No      | Yes      | No       |
| nanoCLR    | Secondary | -       | No    | No     | No        | No      | No       | No       |
| Deployment | Primary   | 1.0.0   | Yes   | Yes    | Yes       | No      | Yes      | No       |
| Deployment | Secondary | -       | No    | No     | No        | No      | No       | No       |
+------------+-----------+---------+-------+--------+-----------+---------+----------+----------+
```

(The values above are illustrative.) An empty or `null` array prints `(no images)`.

The [IFU_Management sample](../Samples/IFU_Management/Program.cs) shows all of these calls.

## Recipes

### Which version am I running?

```csharp
ImageInfo running = UpdateManager.GetPrimaryImageInfo(ImageType.Deployment);
Version current = running?.Version;
```

### Is an update needed?

Compare the version the provider offers with the running one:

```csharp
bool updateNeeded = running == null || IsNewer(offered, running.Version);

// ...

private static bool IsNewer(Version a, Version b)
{
    if (a.Major != b.Major) return a.Major > b.Major;
    if (a.Minor != b.Minor) return a.Minor > b.Minor;
    if (a.Build != b.Build) return a.Build > b.Build;
    return a.Revision > b.Revision;
}
```

The provider's version must match the version in the image's MCUboot header, which is what
`ImageInfo.Version` reports after the swap.

### Is the device ready to stage a new image?

```csharp
bool ready =
    UpdateManager.GetStatus(ImageType.Deployment) == UpdateStatus.Confirmed
    && UpdateManager.GetUpdateSessionOwner(ImageType.Deployment) == UpdateSessionOwner.None;
```

### Is an update already staged and waiting for a reboot?

```csharp
ImageInfo staged = UpdateManager.GetSecondaryImageInfo(ImageType.Deployment);
bool waitingForReboot = staged != null && staged.IsPending;
```

### Did the last update get rolled back?

After a reboot in which the new image was not confirmed, the primary slot again holds the old
version and the secondary slot holds the rejected one. Comparing `GetPrimaryImageInfo(...).Version`
against the version you last staged (kept by your application, for example in a file) tells you the
update did not stick.

# Unit tests

Tests for `nanoFramework.Runtime.InFieldUpdate`, focused on the update session API
(`StartUpdateSession` / `ResumeUpdateSession` / `StoreImageChunk` / `CompleteUpdateSession` /
`AbortUpdateSession`).

## These tests need real hardware

The IFU native assembly only exists on MCUboot-enabled targets, so the tests cannot run on the
Windows nanoCLR. Run them on a device running an MCUboot firmware built with
`API_nanoFramework.Runtime.InFieldUpdate` (for example ORGPAL_PALTHREE), with `.runsettings` at
the repository root selecting real hardware. That is also why `runUnitTests` stays `false` in
`azure-pipelines.yml`.

## What they touch

Only the **deployment** image's secondary slot. The nanoCLR secondary slot is never written: it
may hold the image the device would roll back to. Each class erases the slot before and after its
tests, so no staged image or scheduled swap survives a run.

The test images are built in RAM by `Helpers/McuBootImageBuilder`: a real MCUboot header, a
deterministic payload and a TLV area with a SHA-256 computed by `Helpers/Sha256` (nanoFramework
has no managed SHA-256). They are unsigned, which is enough because completing a session checks
structure and hash only - MCUboot itself would reject an unsigned image at boot and erase the
slot.

## Provider flow

`ProviderFlowTests` drive the whole update flow the way an agent does - resume or start,
download, complete - through `Helpers/ReferenceAgent` (the skeleton from
`docs/writing-an-update-provider.md`) and `Helpers/FakeUpdateProvider`, an `IUpdateProvider` that
serves a test image from RAM and can drop the connection at a given offset, serve a corrupt image or
offer nothing. They cover a full download, `ChunkSize`, pause and resume after a transport failure,
the `MaxRetries` cap on a corrupt package, a different image left in the slot, and nothing on offer.

`UpdateAgentOptionsTests` check the option defaults and validation; they need no IFU support.

## What is not covered

Needs a reboot or a second writer, so it is verified by hand:

- resuming after a hard reset in the middle of a download (reset the board between chunks, then
  call `ResumeUpdateSession`)
- a CLR-only restart releasing the open sessions (deploy from Visual Studio mid-download, then
  check `GetUpdateSessionOwner` reports `None` and `ResumeUpdateSession` still works)
- a Wire Protocol write colliding with an open managed session (`nanoff` should report error 9,
  `Monitor_Image_Error_Busy`)
- interaction with images imported from SD card or USB by the bootloader
- `SwapInFlight`, and flash read/write failures
- the `ConfirmHook` path of an agent: it needs the deployment image in `Testing`, which only a
  real swap and reboot produce (stage `IFU-blink-app`, reboot, then run an agent with a hook
  returning `true` and check the image is confirmed; repeat with `false` and check it reverts on
  the next reboot)

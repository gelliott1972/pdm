# Brief: "Used by" on the PDM page

From Admin, 13 Nov 2026. Owner: **App**. Grant: "go with the app". It came from James: "In the PDM he can't click on a part and find where it is used."

## What

When someone clicks a file on the page (part, assembly or drawing), show a **Used by** list: every vault file whose references include it, with its folder. Each name is clickable and takes you to that file. Show "Used by: nothing" when empty, and, for assemblies and parts, also **Uses** (its own references) if that's cheap.

## Notes

- The data is already there: each record's `refs` and the page's `usedBy` (used today for the remove check and the "shared, used by n other assemblies" tag). Leave removed files out.
- Drawings count as users (a part used by its drawing shows the drawing).
- Plain look, the same as the rest of the page.
- Ship it as the next version. Grant publishes by typing "publish x.y.z" in the pdm-app window himself (a relayed OK is blocked). Tell Admin the version number when it's ready.

# Weldment profiles: File Locations (brief sw-profiles step 4)

The profile library is now in the vault at `P:\_Shared\_Profiles` (same layout as `M:\DLAPEX_Members`: standard **DLAPEX**, types **ea**, **rhs**, **shs**).
The vault parts already point there. This setting only decides where SOLIDWORKS looks when someone **adds a new** structural member.

Do this once on each PC (Grant's, then James's), with SOLIDWORKS 2026 open:

1. Click **Tools > Options**.
2. On the **System Options** tab, click **File Locations** (left-hand list).
3. In **Show folders for**, pick **Weldment Profiles**.
4. In the **Folders** list, click the line `M:\DLAPEX_Members`.
5. Click **Delete**. Leave the SOLIDWORKS built-in line (`...\lang\english\weldment profiles`) where it is.
6. Click **Add...**, browse to `P:\_Shared\_Profiles`, click **Select Folder**.
7. Click **OK**. If SOLIDWORKS asks "Would you like to make the following changes to your search paths?", click **Yes**.
8. Check: **Insert > Weldments > Structural Member**. In the PropertyManager, **Standard** should offer **DLAPEX**, and **Type** should offer **ea**, **rhs** and **shs**.

If **DLAPEX** doesn't show in step 8, close SOLIDWORKS, open it again, and repeat step 8.

For James's PC: if his profiles folder isn't `M:\DLAPEX_Members`, delete whichever line points to his copy of DLAPEX_Members in step 4 instead.

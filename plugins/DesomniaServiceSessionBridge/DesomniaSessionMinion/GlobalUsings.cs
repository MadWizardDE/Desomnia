// The classic Win32 menu API comes from the vendored WinFormsLegacyControls fork.
// The aliases outrank both namespace imports and the throwing binary-compat stubs
// that .NET 10 re-added to System.Windows.Forms, so the menu code compiles unchanged.
global using System.Windows.Forms.Legacy;

global using ContextMenu = System.Windows.Forms.Legacy.ContextMenu;
global using Menu = System.Windows.Forms.Legacy.Menu;
global using MenuItem = System.Windows.Forms.Legacy.MenuItem;

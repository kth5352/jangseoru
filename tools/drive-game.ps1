# Input-automation helpers used for verification (dot-source this file while the game is running).
# SAFETY RULES (user instruction): only save slots 009 and 010 may be loaded or saved; never click other slots;
# avoid anything that triggers auto-saves. Every input call first checks that the game window
# (class UnityWndClass, NOT the BepInEx console window) is in the foreground and aborts otherwise.
# Coordinates are client-area pixels of the 2560x1440 game window. Screenshots go next to this script.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Drawing; using System.Drawing.Imaging;
public static class Win {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, UIntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
  public static void Shot(IntPtr h, string path) {
    RECT c; GetClientRect(h, out c); POINT p; p.X=0; p.Y=0; ClientToScreen(h, ref p);
    int w=c.R-c.L, ht=c.B-c.T;
    using (var bmp = new Bitmap(w, ht)) { using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(p.X, p.Y, 0, 0, new Size(w, ht)); bmp.Save(path, ImageFormat.Png); }
  }
  public static void ClickClient(IntPtr h, int x, int y) {
    POINT p; p.X=x; p.Y=y; ClientToScreen(h, ref p);
    SetCursorPos(p.X, p.Y); System.Threading.Thread.Sleep(250);
    mouse_event(0x0002,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(90);
    mouse_event(0x0004,0,0,0,UIntPtr.Zero);
  }
  public static void Key(byte vk) { keybd_event(vk,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(80); keybd_event(vk,0,2,UIntPtr.Zero); }
}
"@
[Win]::SetProcessDPIAware() | Out-Null
Add-Type -TypeDefinition @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class Finder {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  public static IntPtr Unity(uint target) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h,l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid==target && IsWindowVisible(h)) { var c=new StringBuilder(256); GetClassName(h,c,256); if (c.ToString()=="UnityWndClass") { found=h; return false; } } return true; }, IntPtr.Zero);
    return found;
  }
}
"@
function Find-GameWindow($processId) { $w = [Finder]::Unity([uint32]$processId); if ($w -eq [IntPtr]::Zero) { throw 'game window (UnityWndClass) not found' }; return $w }
$script:proc = Get-Process -Name Mortal -ErrorAction Stop | Select-Object -First 1
$script:h = Find-GameWindow $script:proc.Id
function Focus-Game { [Win]::SetForegroundWindow($script:h) | Out-Null; Start-Sleep -Milliseconds 500; if ([Win]::GetForegroundWindow() -ne $script:h) { throw 'game window is not in the foreground; aborting before sending input' } }
function Shot($name) { [Win]::Shot($script:h, "$PSScriptRoot\$name.png") }
function Click($x,$y) { Focus-Game; [Win]::ClickClient($script:h, $x, $y) }
function Press($vk) { Focus-Game; [Win]::Key([byte]$vk) }

function Wheel($x,$y,$ticks) { Focus-Game; $p = New-Object Win+POINT; $p.X=$x; $p.Y=$y; [Win]::ClientToScreen($script:h, [ref]$p) | Out-Null; [Win]::SetCursorPos($p.X,$p.Y) | Out-Null; Start-Sleep -Milliseconds 300; for($i=0;$i -lt [math]::Abs($ticks);$i++){ $d = if($ticks -lt 0){ [UInt32]::Parse('4294967176') } else { [UInt32]120 }; [Win]::mouse_event(0x0800,0,0,$d,[UIntPtr]::Zero); Start-Sleep -Milliseconds 250 } }
function Drag($x1,$y1,$x2,$y2) { Focus-Game; $p = New-Object Win+POINT; $p.X=$x1; $p.Y=$y1; [Win]::ClientToScreen($script:h, [ref]$p) | Out-Null; [Win]::SetCursorPos($p.X,$p.Y) | Out-Null; Start-Sleep -Milliseconds 300; [Win]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 200; $steps=12; for($i=1;$i -le $steps;$i++){ $nx=$p.X + [int](($x2-$x1)*$i/$steps); $ny=$p.Y + [int](($y2-$y1)*$i/$steps); [Win]::SetCursorPos($nx,$ny) | Out-Null; Start-Sleep -Milliseconds 80 }; Start-Sleep -Milliseconds 200; [Win]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero) }

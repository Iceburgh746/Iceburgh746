#!/usr/bin/env python3
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
radio=(ROOT/'src'/'RadioSession.cs').read_text()
controls=(ROOT/'src'/'Controls.cs').read_text()
main=(ROOT/'src'/'MainForm.cs').read_text()
build=(ROOT/'Build-Windows.cmd').read_text()
assert 'DataReceived +=' not in radio
assert 'private void ReaderLoop()' in radio
assert 'sp.DtrEnable = true' in radio
assert 'Write(ViewerProtocol.MakeViewerKeepAlive());' in radio
assert 'Write(ViewerProtocol.MakeKeepAlive(restart));' in radio
assert 'System.Threading.Interlocked.Exchange(ref repaintPending, 1)' in controls
assert 'Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)' in controls
assert 'SetPixel(' not in controls
assert 'ConsoleFaceplate.png' in main
assert 'LoadFaceplateImage' in main
assert 'ShowBezel = false' in main and 'ForceFill = true' in main
assert 'Panel lcdHost = new Panel' in main
assert 'Size = new Size(424, 212)' in main
assert 'Location = new Point(2, 48)' in main
assert 'ScreenMargin' not in controls
assert 'AddFaceKey' in main
assert '38400' in main
assert '/resource:"assets\\ConsoleFaceplate.png",UVConsole.ConsoleFaceplate.png' in build
print('ALL SOURCE AUDIT TESTS PASSED')

import glob
from pyzbar.pyzbar import decode
from PIL import Image
ok = bad = 0
for png in sorted(glob.glob('qr/*.png')):
    want = open(png[:-4] + '.txt', encoding='utf-8').read()
    r = decode(Image.open(png))
    got = r[0].data.decode('utf-8') if r else ''
    if got == want: ok += 1
    else: bad += 1
    print("ZBAR", png, "OK" if got == want else "MISMATCH", len(want), len(got), flush=True)
print("ZBAR passed", ok, "failed", bad, flush=True)

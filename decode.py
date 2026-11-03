import glob
from pyzbar.pyzbar import decode
from PIL import Image
for png in sorted(glob.glob('qr/win-*.png')):
    r = decode(Image.open(png))
    print("WINQR", png, r[0].data.decode('utf-8')[:80] if r else "NOT FOUND", flush=True)

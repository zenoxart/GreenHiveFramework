"""Builds a multi-size Windows .ico (PNG frames, 16-256 px) from an SVG. Usage: make-icon.py <logo.svg> <out.ico>"""
import struct, subprocess, sys
svg, out = sys.argv[1], sys.argv[2]
sizes = [16,20,24,32,40,48,64,96,128,256]
pngs = [subprocess.run(["rsvg-convert","-w",str(s),"-h",str(s),svg],capture_output=True,check=True).stdout for s in sizes]
hdr = struct.pack("<HHH",0,1,len(sizes)); off = 6+16*len(sizes); ents=b""
for s,p in zip(sizes,pngs):
    ents += struct.pack("<BBBBHHII", s%256, s%256, 0,0,1,32,len(p),off); off += len(p)
open(out,"wb").write(hdr+ents+b"".join(pngs))

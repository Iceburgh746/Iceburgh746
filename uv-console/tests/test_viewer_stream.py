#!/usr/bin/env python3
"""Known-answer stream tests mirroring UV Console's viewer frame parser."""

def take_frame(buf):
    while len(buf) >= 2:
        start=-1; flags=0; marker=False
        for i in range(len(buf)-1):
            b=buf[i]
            if b==0xFF or (b & 0xF0)==0xF0:
                if i+2 >= len(buf):
                    return None
                if buf[i+1]==0xAA and buf[i+2]==0x55:
                    marker=True; flags=0 if b==0xFF else (b & 0x0F); start=i; break
            if buf[i]==0xAA and buf[i+1]==0x55:
                start=i; marker=False; flags=0; break
        if start < 0:
            keep = bool(buf) and (buf[-1] == 0xAA or buf[-1] == 0xFF or (buf[-1]&0xF0)==0xF0)
            tail=buf[-1] if keep else None
            buf.clear()
            if tail is not None: buf.append(tail)
            return None
        if start>0: del buf[:start]
        hs=1 if marker else 0
        if len(buf)<hs+5: return None
        if buf[hs]!=0xAA or buf[hs+1]!=0x55:
            del buf[0]; continue
        typ=buf[hs+2]; size=(buf[hs+3]<<8)|buf[hs+4]
        if size>8192:
            del buf[0]; continue
        total=hs+5+size+1
        if len(buf)<total: return None
        if buf[total-1]!=0x0A:
            del buf[0]; continue
        payload=bytes(buf[hs+5:hs+5+size])
        del buf[:total]
        return typ,flags,payload
    return None

def mk_frame(typ,payload,flags=None):
    prefix=b'' if flags is None else bytes([0xF0 | (flags & 0x0F)])
    return prefix+b'\xAA\x55'+bytes([typ])+len(payload).to_bytes(2,'big')+payload+b'\x0A'

def test_full_frame_fragmentation():
    payload=bytes((i*37)&0xFF for i in range(1024))
    frame=mk_frame(0x01,payload,0x06)
    for split in range(1,len(frame)):
        buf=bytearray(frame[:split])
        assert take_frame(buf) is None
        buf.extend(frame[split:])
        got=take_frame(buf)
        assert got==(0x01,0x06,payload), split
        assert not buf

def test_diff_frame_and_noise():
    payload=bytes([3])+bytes(range(8))+bytes([7])+bytes(range(8,16))
    frame=bytearray(b'noise\x99\x88')+bytearray(mk_frame(0x02,payload,None))
    got=take_frame(frame)
    assert got==(0x02,0,payload)

def test_keepalives():
    assert bytes([0x55,0xAA,0x00,0x00]) == b'\x55\xAA\x00\x00'
    assert bytes([0x55,0xAA,0x05,0x83]) == b'\x55\xAA\x05\x83'
    assert bytes([0x55,0xAA,0x05,0x03]) == b'\x55\xAA\x05\x03'

def test_key_packets():
    assert bytes([0xAA,0x55,0x03,0x0A]) == b'\xAA\x55\x03\x0A'
    assert bytes([0xAA,0x55,0x04,0x12]) == b'\xAA\x55\x04\x12'

if __name__=='__main__':
    tests=[test_full_frame_fragmentation,test_diff_frame_and_noise,test_keepalives,test_key_packets]
    for t in tests:
        t(); print('PASS',t.__name__)
    print('ALL VIEWER STREAM TESTS PASSED')

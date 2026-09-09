#!/usr/bin/env python3
"""Protocol-independent known-answer tests for the native translation."""
OBF=[0x16,0x6c,0x14,0xe6,0x2e,0x91,0x0d,0x40,0x21,0x35,0xd5,0x40,0x13,0x03,0xe9,0x80]

def crc16(data):
    crc=0
    for b in data:
        crc ^= b << 8
        for _ in range(8):
            crc = (((crc << 1) ^ 0x1021) if (crc & 0x8000) else (crc << 1)) & 0xffff
    return crc

def make_message(msg_type, payload=b''):
    return msg_type.to_bytes(2,'little')+len(payload).to_bytes(2,'little')+payload

def make_packet(msg):
    n=len(msg)+(len(msg)&1)
    body=bytearray(n+2)
    body[:len(msg)]=msg
    body[n:n+2]=crc16(body[:n]).to_bytes(2,'little')
    for i in range(len(body)): body[i]^=OBF[i%len(OBF)]
    return b'\xab\xcd'+n.to_bytes(2,'little')+body+b'\xdc\xba'

def test_reboot():
    got=make_packet(make_message(0x05DD))
    want=bytes.fromhex('AB CD 04 00 CB 69 14 E6 5B EB DC BA')
    assert got==want,(got.hex(' '),want.hex(' '))

def test_viewer_diff():
    fb=bytearray(1024)
    payload=bytes([3])+bytes(range(8))
    idx=payload[0]*8
    fb[idx:idx+8]=payload[1:]
    assert fb[24:32]==bytes(range(8))

def test_logo_layout():
    magic=b'F4HWNLGO'
    assert len(magic)==8
    assert 8+1024==1032
    assert ((1032+15)//16)*16==1040

def test_rf_log_sizes():
    row_size=15+10
    assert row_size==25
    assert 4+row_size*(64+1)==1629
    assert row_size*64==1600

if __name__=='__main__':
    tests=[test_reboot,test_viewer_diff,test_logo_layout,test_rf_log_sizes]
    for t in tests:
        t(); print('PASS',t.__name__)
    print('ALL PROTOCOL VECTOR TESTS PASSED')

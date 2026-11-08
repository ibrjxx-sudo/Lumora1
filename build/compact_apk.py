"""Repack model assets losslessly; native libraries stay stored and require zipalign + signing afterwards."""
from pathlib import Path
import sys,zipfile,shutil,copy
source,target=map(Path,sys.argv[1:3])
if source.resolve()==target.resolve():raise ValueError('Use a new output path.')
with zipfile.ZipFile(source) as original,zipfile.ZipFile(target,'w',allowZip64=True,compresslevel=6) as compact:
    for entry in original.infolist():
        name=entry.filename
        if name.startswith('META-INF/') and (name.endswith(('.RSA','.DSA','.EC','.SF')) or name=='META-INF/MANIFEST.MF'):continue
        item=copy.copy(entry);item.extra=b''
        if name.startswith('assets/Models/'):item.compress_type=zipfile.ZIP_DEFLATED
        with original.open(entry) as reader,compact.open(item,'w') as writer:shutil.copyfileobj(reader,writer,1024*1024)
print(f'Repacked model assets without changing their bytes: {source.stat().st_size} -> {target.stat().st_size}')

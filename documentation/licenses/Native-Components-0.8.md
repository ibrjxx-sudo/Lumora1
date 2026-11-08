# Components in Lumora 0.8

All previous 0.7 notices and source snapshots are retained in this folder and `../vendor-source/`. Refer to `Native-Components.md` for LibRaw, FFmpeg, whisper.cpp, ONNX Runtime, SkiaSharp, YuNet, Silero, LaMa, BiRefNet and Microsoft runtime origins.

The new 4K tool uses **Real-ESRGAN General x4v3**, originally by Xintao Wang and contributors, BSD 3-Clause. The supplied ONNX conversion was published by Qualcomm AI Hub. Inference runs through the existing ONNX Runtime CPU dependency; it does not call Qualcomm or another hosted service.

- Original implementation and weights: <https://github.com/xinntao/Real-ESRGAN>
- Original license: `Real-ESRGAN-LICENSE.txt`
- Conversion: <https://huggingface.co/qualcomm/Real-ESRGAN-General-x4v3>
- Pinned file: <https://huggingface.co/qualcomm/Real-ESRGAN-General-x4v3/resolve/fe119b62b7cb27c3fa5fd3d992122f8e5a3a52c4/Real-ESRGAN-General-x4v3.onnx>
- Distributed name: `Models/upscale4x.onnx`
- Size: 4,872,481 bytes
- SHA-256: `bea98dff749104082449888501454b85a05ace135c89249599caec7ef2dace23`

Effects controls were checked against Adobe's published descriptions, with independent Lumora processing: <https://helpx.adobe.com/lightroom/desktop/edit-photos/edit-photos.html>. No proprietary Adobe/CapCut code, camera profiles, models or asset packs are included.

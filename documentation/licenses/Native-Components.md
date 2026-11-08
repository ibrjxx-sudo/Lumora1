# Component origins and licenses — 0.7

Lumora is independent C#/WPF software. It does not contain Adobe/CapCut source code, proprietary transition assets, paid filter packs, or Adobe camera profiles. Windows fonts are referenced from the OS, not redistributed.

Native components:

- [LibRaw 0.22.3](https://www.libraw.org/download), dynamically loaded. LGPL 2.1 / CDDL notices supplied. Original unmodified Windows archive with source is included under `documentation/vendor-source/`. Archive SHA-256: `4e2213a0b10e32199a30b9114a61376cdb40f94df861262b4a6dca659701276b`.
- [Gyan FFmpeg 9.0.2 essentials](https://www.gyan.dev/ffmpeg/builds/), GPL v3, invoked as separate processes. Original license, README/build configuration and source reference are supplied. Source commit: `946fcce07b`. Archive SHA-256: `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba`.
- [whisper.cpp b5454 / 1.9.5](https://github.com/ggml-org/whisper.cpp/releases/tag/b5454), MIT. CPU archive SHA-256: `6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb`.
- ONNX Runtime 1.30.0 and SkiaSharp 4.154.0: existing NuGet component notices retained.
- [Microsoft Visual C++ x64 redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe): supplied signed by Microsoft, not executed during authoring. Microsoft terms apply when the user runs it. SHA-256: `cc0ff0eb1dc3f5188ae6300faef32bf5beeba4bdd6e8e445a9184072096b713b`.

Local model weights:

| Model | Source | SHA-256 |
|---|---|---|
| Whisper Large v3 Turbo Q5_0 · MIT | [ggerganov/whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp) | `394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2` |
| Silero VAD 6.2.0 · MIT | [ggml-org/whisper-vad](https://huggingface.co/ggml-org/whisper-vad) | `2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987` |
| YuNet 2023mar · supplied license | [OpenCV Zoo](https://github.com/opencv/opencv_zoo/tree/main/models/face_detection_yunet) | `8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4` |
| LaMa ONNX · Apache 2.0 | [Carve/LaMa-ONNX](https://huggingface.co/Carve/LaMa-ONNX), based on [Roman Suvorov et al., LaMa](https://github.com/advimman/lama); ONNX conversion by Carve; credit Places2 authors for training data | `1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6` |
| BiRefNet Lite · existing notices | [studioludens conversion](https://huggingface.co/studioludens/birefnet-lite-512/tree/962e67969ec6acec9c19fab76acee4f5e22e990c) | `1cb0fb360dadd15af77c639085d77a9df67db0c64315560c3de005f676345ac2` |

Model weights are optional in the basic package and present in the full package. They make no network requests during inference. Licenses and prior image/background credits are retained beside this notice.

Test-only fixtures came from [rawpy tests](https://github.com/letmaik/rawpy/tree/main/test), [OpenCV samples](https://github.com/opencv/opencv/tree/master/samples/data), and [Google FLEURS](https://huggingface.co/datasets/google/fleurs) (`ar_eg`, test row 0, CC BY 4.0). They are not added to the distributed demo library. The Arabic validation reports errors and does not imply universal dialect accuracy.

XMP follows the [Adobe Camera Raw namespace](https://developer.adobe.com/xmp/docs/xmp-namespaces/crs/) and [published example](https://github.com/AdobeDocs/cis-photoshop-api-docs/blob/main/sample-code/lr-sample-app/crs.xml). Appearance is not certified against Lightroom. [FFmpeg filter documentation](https://ffmpeg.org/ffmpeg-filters.html) describes the transition/subtitle primitives.

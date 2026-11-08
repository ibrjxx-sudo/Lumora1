# Lumora 0.9.12 — بناء نسخة iPhone

هذه ملفات بناء وليست تطبيق IPA جاهزًا. لم تتوفر بيئة macOS أو شهادة Apple Developer في جلسة تجهيز الإصدار، لذلك لم يُبنَ هذا الهدف ولم يُختبر على iPhone. يتضمن المصدر نفس بطاقة المطوّر الاختيارية ورابط Instagram وحقوق النشر والشعار الجديد وتحريك النص.

## المطلوب

1. Mac مع Xcode وأدواته، وإصدار Xcode المتوافق مع حمل عمل .NET iOS المثبّت. المشروع يستخدم .NET SDK المحدد في `global.json`، و.NET MAUI 10 وiOS 18 أو أحدث.
2. تثبيت .NET SDK والحمل `maui-ios`، وCMake وNinja لبناء جسر RAW والصوت الأصلي. ثبّت المتطلبات من مصادرها الرسمية قبل تشغيل السكربت.
3. شهادة Apple Development أو Apple Distribution ومفتاحها في Keychain على Mac، مع provisioning profile مطابق لمعرف `com.ibrahimabuhamad.lumora`. قد تحتاج لتغيير المعرف إن لم يكن مملوكًا لحساب Apple المستخدم.
4. للتثبيت المباشر Development أو Ad Hoc يجب تسجيل الجهاز وتضمينه في ملف provisioning. لا يكفي امتلاك IPA لتثبيته على أي جهاز. App Store/TestFlight يحتاجان قناة التوزيع المناسبة والمراجعة أو إعدادات Apple الخاصة بها.

## البناء

افتح مجلد المصدر على Mac. لا تضع مفاتيح Apple أو كلمة مرورها داخل المصدر أو ترسلها في المحادثة. الاسمان في المتغيرين التاليين اسما الشهادة والملف المثبتين محليًا، وليسا محتوى المفاتيح:

```bash
export LUMORA_CODESIGN_KEY='Apple Development: Your Name (TEAMID)'
export LUMORA_CODESIGN_PROFILE='Your provisioning profile name'
bash build/Publish-iPhone.sh
```

يبني السكربت المكتبة الأصلية من المصدر داخل `Native.Mobile` ثم ينشر هدف `ios-arm64`. يطبع مسار IPA الناتج إن اكتمل البناء. لا تستعمل مكتبة Android مع iOS؛ يُعاد بناء الجسر الأصلي خصيصًا لـiOS.

النموذج الخفيف للعزل مضمن حيث تسمح موارد المشروع. حزمة النماذج الاختيارية متاحة للاستيراد من واجهة Lumora للأدوات الأكبر. راجع مساحة الحزمة والذاكرة وترخيص كل اعتماد ومتطلبات الخصوصية الحالية قبل توزيع التطبيق.

بعض اعتمادات MAUI/Skia/FFmpegKit أو سياسات Apple قد تحتاج تعديلًا عند أول بناء حقيقي. هذه الحزمة لا تدعي نجاح بناء iOS غير منفذ. افحص فتح التطبيق، الكاميرا، الاستيراد والحفظ، النص العربي، المعالجة، وظهور بطاقة المطوّر مرة واحدة على جهاز حقيقي قبل التوزيع.

## التوقيع والحماية

يعتمد iPhone على توقيع Apple وملف التزويد. فحص توقيع Android لا يعمل على iOS، وملف سلامة Windows ليس بديلًا عن توقيع Apple. لا توجد آلية تجعل التطبيق محصنًا من كل محاولات الهندسة العكسية.

مراجع رسمية:

- [متطلبات نشر .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/deployment)
- [بناء وتوقيع IPA من سطر الأوامر](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/publish-cli?view=net-maui-10.0)
- [التوزيع على أجهزة مسجلة لدى Apple](https://developer.apple.com/documentation/xcode/distributing-your-app-to-registered-devices)

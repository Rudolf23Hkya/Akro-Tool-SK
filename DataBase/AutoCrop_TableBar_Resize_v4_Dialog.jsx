#target photoshop
app.bringToFront();

(function () {

    // -------------------- Settings Dialog --------------------
    function showSettingsDialog() {
        var w = new Window("dialog", "Auto Crop + Resize (Batch)");

        w.orientation = "column";
        w.alignChildren = "fill";

        // Folders panel
        var pFolders = w.add("panel", undefined, "Folders");
        pFolders.orientation = "column";
        pFolders.alignChildren = "fill";

        function folderRow(label) {
            var g = pFolders.add("group");
            g.orientation = "row";
            g.alignChildren = ["fill", "center"];

            g.add("statictext", undefined, label).preferredSize.width = 90;
            var t = g.add("edittext", undefined, "");
            t.characters = 40;
            var b = g.add("button", undefined, "Browse…");
            return { group: g, text: t, btn: b };
        }

        var inRow = folderRow("Input:");
        var outRow = folderRow("Output:");

        inRow.btn.onClick = function () {
            var f = Folder.selectDialog("Select INPUT folder");
            if (f) inRow.text.text = f.fsName;
        };
        outRow.btn.onClick = function () {
            var f = Folder.selectDialog("Select OUTPUT folder");
            if (f) outRow.text.text = f.fsName;
        };

        // Naming panel
        var pName = w.add("panel", undefined, "Naming");
        pName.orientation = "row";
        pName.alignChildren = ["left", "center"];

        pName.add("statictext", undefined, "Suffix:");
        var tSuffix = pName.add("edittext", undefined, "");
        tSuffix.characters = 20;

        // Crop panel
        var pCrop = w.add("panel", undefined, "Crop (px)");
        pCrop.orientation = "column";
        pCrop.alignChildren = "left";

        function numberRow(panel, label, defVal) {
            var g = panel.add("group");
            g.orientation = "row";
            g.alignChildren = ["left", "center"];
            var st = g.add("statictext", undefined, label);
            st.preferredSize.width = 220;
            var et = g.add("edittext", undefined, String(defVal));
            et.characters = 8;
            return et;
        }

        // ✅ Defaults = 4
        var tLeftTrim      = numberRow(pCrop, "Trim LEFT edge:", 4);
        var tRightTrim     = numberRow(pCrop, "Trim RIGHT edge:", 4);
        var tTopTrim       = numberRow(pCrop, "Trim TOP edge:", 4);
        var tPadAboveLine  = numberRow(pCrop, "Extra remove ABOVE separator line:", 4);

        // Resize panel
        var pResize = w.add("panel", undefined, "Resize");
        pResize.orientation = "column";
        pResize.alignChildren = "left";

        var tTargetW = numberRow(pResize, "Target WIDTH (px):", 1032);
        var tTargetH = numberRow(pResize, "Target HEIGHT (px):", 536);

        var gMode = pResize.add("group");
        gMode.orientation = "row";
        gMode.alignChildren = ["left", "center"];
        var stMode = gMode.add("statictext", undefined, "Mode:");
        stMode.preferredSize.width = 220;

        var ddMode = gMode.add("dropdownlist", undefined, [
            "1 - FIT inside (keeps aspect, sizes vary)",
            "2 - FILL + center crop (uniform, no stretch) [recommended]",
            "3 - STRETCH to exact (uniform, distorts)"
        ]);
        ddMode.selection = 1;

        // Buttons
        var gBtns = w.add("group");
        gBtns.alignment = "right";
        var bCancel = gBtns.add("button", undefined, "Cancel");
        var bOK = gBtns.add("button", undefined, "OK");
        bOK.active = true;

        bCancel.onClick = function () { w.close(0); };

        function toNum(s, fallback) {
            var n = Number(String(s).replace(",", "."));
            return (isNaN(n) ? fallback : n);
        }

        bOK.onClick = function () {
            // Validate folders
            var inPath = inRow.text.text;
            var outPath = outRow.text.text;

            if (!inPath || !Folder(inPath).exists) {
                alert("Please select a valid INPUT folder.");
                return;
            }
            if (!outPath || !Folder(outPath).exists) {
                alert("Please select a valid OUTPUT folder.");
                return;
            }
            w.close(1);
        };

        var result = w.show();
        if (result !== 1) return null;

        // Build settings object
        var modeText = ddMode.selection ? ddMode.selection.text : "";
        var mode = "2";
        if (modeText.indexOf("1") === 0) mode = "1";
        else if (modeText.indexOf("3") === 0) mode = "3";

        return {
            inFolder: Folder(inRow.text.text),
            outFolder: Folder(outRow.text.text),
            suffix: tSuffix.text,

            leftTrim: Math.max(0, Math.round(toNum(tLeftTrim.text, 4))),
            rightTrim: Math.max(0, Math.round(toNum(tRightTrim.text, 4))),
            topTrim: Math.max(0, Math.round(toNum(tTopTrim.text, 4))),
            padAboveLine: Math.max(0, Math.round(toNum(tPadAboveLine.text, 4))),

            targetW: Math.max(50, Math.round(toNum(tTargetW.text, 1032))),
            targetH: Math.max(50, Math.round(toNum(tTargetH.text, 536))),
            mode: mode
        };
    }

    var S = showSettingsDialog();
    if (!S) return;

    // -------------------- Helpers --------------------
    function getExt(f) {
        var m = f.name.match(/\.([^.]+)$/);
        return m ? m[1].toLowerCase() : "";
    }

    function baseName(f) {
        return decodeURI(f.name).replace(/\.[^\.]+$/, "");
    }

    function saveWithSameFormat(doc, srcFile, dstFile) {
        var ext = getExt(srcFile);

        if (ext === "jpg" || ext === "jpeg") {
            var oJ = new JPEGSaveOptions();
            oJ.quality = 12;
            doc.saveAs(dstFile, oJ, true, Extension.LOWERCASE);
        } else if (ext === "png") {
            var oP = new PNGSaveOptions();
            doc.saveAs(dstFile, oP, true, Extension.LOWERCASE);
        } else if (ext === "tif" || ext === "tiff") {
            var oT = new TiffSaveOptions();
            oT.imageCompression = TIFFEncoding.NONE;
            doc.saveAs(dstFile, oT, true, Extension.LOWERCASE);
        } else {
            var oP2 = new PNGSaveOptions();
            var forced = new File(dstFile.fsName.replace(/\.[^.]+$/, "") + ".png");
            doc.saveAs(forced, oP2, true, Extension.LOWERCASE);
        }
    }

    // <= 9 samplers avoids "Make not available" (sampler cap)
    function makeRowLuminanceFn(doc, w, startY, samples) {
        try { doc.colorSamplers.removeAll(); } catch (e0) {}

        var samplers = [];
        var xs = [];

        for (var i = 0; i < samples; i++) {
            var x = Math.round((i + 0.5) * (w / samples));
            if (x < 1) x = 1;
            if (x > w - 1) x = w - 1;
            xs.push(x);
            samplers.push(doc.colorSamplers.add([x, startY])); // numbers only
        }

        function lumAtY(y) {
            var sum = 0;
            for (var i = 0; i < samplers.length; i++) {
                samplers[i].position = [xs[i], y];
                var c = samplers[i].color.rgb;
                sum += (0.2126 * c.red + 0.7152 * c.green + 0.0722 * c.blue);
            }
            return sum / samplers.length;
        }

        function cleanup() {
            try { doc.colorSamplers.removeAll(); } catch (e) {}
        }

        return { lumAtY: lumAtY, cleanup: cleanup };
    }

    function findSeparatorTopY(doc) {
        var w = Math.round(doc.width.as("px"));
        var h = Math.round(doc.height.as("px"));

        var searchStart = Math.round(h * 0.40);
        var searchEnd   = Math.round(h * 0.95);

        var samples = 9;
        var rowFn = makeRowLuminanceFn(doc, w, searchStart, samples);

        var bestY = searchStart;
        var bestLum = 1e9;

        for (var y = searchStart; y <= searchEnd; y += 4) {
            var L = rowFn.lumAtY(y);
            if (L < bestLum) { bestLum = L; bestY = y; }
        }

        var refineFrom = Math.max(searchStart, bestY - 10);
        var refineTo   = Math.min(searchEnd,   bestY + 10);

        bestLum = 1e9;
        for (var y2 = refineFrom; y2 <= refineTo; y2 += 1) {
            var L2 = rowFn.lumAtY(y2);
            if (L2 < bestLum) { bestLum = L2; bestY = y2; }
        }

        var threshold = bestLum + 35;
        var top = bestY;
        while (top > 1 && rowFn.lumAtY(top) <= threshold) top--;

        rowFn.cleanup();
        return top + 1;
    }

    function centerCropTo(doc, cropW, cropH) {
        var w = Math.round(doc.width.as("px"));
        var h = Math.round(doc.height.as("px"));

        var left = Math.round((w - cropW) / 2);
        var top  = Math.round((h - cropH) / 2);

        if (left < 0) left = 0;
        if (top < 0) top = 0;

        doc.crop([
            UnitValue(left, "px"),
            UnitValue(top, "px"),
            UnitValue(left + cropW, "px"),
            UnitValue(top + cropH, "px")
        ]);
    }

    function resizePipeline(doc) {
        var w = doc.width.as("px");
        var h = doc.height.as("px");

        if (S.mode === "1") {
            var scaleFit = Math.min(S.targetW / w, S.targetH / h);
            var newW = Math.max(1, Math.round(w * scaleFit));
            var newH = Math.max(1, Math.round(h * scaleFit));
            doc.resizeImage(UnitValue(newW, "px"), UnitValue(newH, "px"), null, ResampleMethod.BICUBICSHARPER);
            return;
        }

        if (S.mode === "3") {
            doc.resizeImage(UnitValue(S.targetW, "px"), UnitValue(S.targetH, "px"), null, ResampleMethod.BICUBICSHARPER);
            return;
        }

        // mode === "2": fill then center-crop
        var scaleFill = Math.max(S.targetW / w, S.targetH / h);
        var filledW = Math.max(1, Math.round(w * scaleFill));
        var filledH = Math.max(1, Math.round(h * scaleFill));

        doc.resizeImage(UnitValue(filledW, "px"), UnitValue(filledH, "px"), null, ResampleMethod.BICUBICSHARPER);
        centerCropTo(doc, S.targetW, S.targetH);
    }

    // -------------------- Main --------------------
    var files = S.inFolder.getFiles(function (f) {
        return (f instanceof File) && (/\.(png|jpg|jpeg|tif|tiff)$/i).test(f.name);
    });

    if (!files || files.length === 0) {
        alert("No image files found in the selected folder.");
        return;
    }

    var originalDialogs = app.displayDialogs;
    app.displayDialogs = DialogModes.NO;

    var errors = [];
    for (var i = 0; i < files.length; i++) {
        var f = files[i];
        try {
            var doc = app.open(f);
            app.activeDocument = doc;

            try { doc.changeMode(ChangeMode.RGB); } catch (e1) {}

            var w0 = Math.round(doc.width.as("px"));
            var h0 = Math.round(doc.height.as("px"));

            var sepTop = findSeparatorTopY(doc);
            var cropBottom = sepTop - S.padAboveLine;
            if (cropBottom < 10) cropBottom = Math.round(h0 * 0.8);

            var left = S.leftTrim;
            var top = S.topTrim;
            var right = w0 - S.rightTrim;
            var bottom = cropBottom;

            // Safety clamps
            if (right <= left + 10) { left = 0; right = w0; }
            if (bottom <= top + 10) { top = 0; bottom = cropBottom; }

            doc.crop([
                UnitValue(left, "px"),
                UnitValue(top, "px"),
                UnitValue(right, "px"),
                UnitValue(bottom, "px")
            ]);

            resizePipeline(doc);

            var ext = getExt(f);
            var outName = baseName(f) + (S.suffix || "") + "." + ext;
            var outFile = new File(S.outFolder.fsName + "/" + outName);

            saveWithSameFormat(doc, f, outFile);
            doc.close(SaveOptions.DONOTSAVECHANGES);

        } catch (err) {
            errors.push(f.name + " → " + err);
            try { app.activeDocument.close(SaveOptions.DONOTSAVECHANGES); } catch (e2) {}
        } finally {
            try { app.activeDocument.colorSamplers.removeAll(); } catch (e3) {}
        }
    }

    app.displayDialogs = originalDialogs;

    if (errors.length) {
        alert("Done, but some files failed:\n\n" + errors.join("\n"));
    } else {
        alert("Done! Cropped + resized " + files.length + " file(s).");
    }

})();
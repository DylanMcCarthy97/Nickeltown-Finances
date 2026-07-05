window.NFCamera = (function () {
  'use strict';

  var stream = null;
  var video = null;
  var guide = null;
  var detectTimer = null;
  var onCapture = null;
  var rotation = 0;

  function haptic() {
    var s = NFSettings.load();
    if (s.haptics && navigator.vibrate) navigator.vibrate(12);
  }

  async function start(videoEl, guideEl, captureCb) {
    video = videoEl;
    guide = guideEl;
    onCapture = captureCb;
    rotation = 0;

    var settings = NFSettings.load();
    var constraints = {
      audio: false,
      video: {
        facingMode: settings.rearCamera ? { ideal: 'environment' } : 'user',
        width: { ideal: 1920 },
        height: { ideal: 1080 }
      }
    };

    try {
      stream = await navigator.mediaDevices.getUserMedia(constraints);
      video.srcObject = stream;
      await video.play();
      startEdgeDetect();
      return true;
    } catch (e) {
      return false;
    }
  }

  function stop() {
    stopEdgeDetect();
    if (stream) {
      stream.getTracks().forEach(function (t) { t.stop(); });
      stream = null;
    }
    if (video) video.srcObject = null;
  }

  function startEdgeDetect() {
    stopEdgeDetect();
    detectTimer = setInterval(function () {
      if (!video || !guide || video.readyState < 2) return;
      try {
        var c = document.createElement('canvas');
        var sw = 80;
        var sh = Math.round(sw * (video.videoHeight / video.videoWidth));
        c.width = sw;
        c.height = sh || 60;
        var ctx = c.getContext('2d');
        ctx.drawImage(video, 0, 0, c.width, c.height);
        var data = ctx.getImageData(0, 0, c.width, c.height).data;
        var edges = 0;
        for (var i = 0; i < data.length; i += 16) {
          var lum = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
          if (lum > 40 && lum < 200) edges++;
        }
        var detected = edges > data.length / 16 * 0.08;
        guide.classList.toggle('detected', detected);
      } catch (e) { /* ignore */ }
    }, 600);
  }

  function stopEdgeDetect() {
    if (detectTimer) { clearInterval(detectTimer); detectTimer = null; }
    if (guide) guide.classList.remove('detected');
  }

  function capture() {
    if (!video) return null;
    haptic();
    var w = video.videoWidth;
    var h = video.videoHeight;
    var canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    canvas.getContext('2d').drawImage(video, 0, 0, w, h);
    var dataUrl = canvas.toDataURL('image/jpeg', 0.92);
    if (onCapture) onCapture(dataUrl);
    return dataUrl;
  }

  function dataUrlToBlob(dataUrl) {
    var parts = dataUrl.split(',');
    var mime = parts[0].match(/:(.*?);/)[1];
    var bin = atob(parts[1]);
    var arr = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
    return new Blob([arr], { type: mime });
  }

  function rotateImage(dataUrl, degrees, cb) {
    var img = new Image();
    img.onload = function () {
      var rad = (degrees * Math.PI) / 180;
      var cw = degrees % 180 === 0 ? img.width : img.height;
      var ch = degrees % 180 === 0 ? img.height : img.width;
      var c = document.createElement('canvas');
      c.width = cw;
      c.height = ch;
      var ctx = c.getContext('2d');
      ctx.translate(cw / 2, ch / 2);
      ctx.rotate(rad);
      ctx.drawImage(img, -img.width / 2, -img.height / 2);
      cb(c.toDataURL('image/jpeg', 0.92));
    };
    img.src = dataUrl;
  }

  function autoCrop(dataUrl, cb) {
    var img = new Image();
    img.onload = function () {
      var w = img.width;
      var h = img.height;
      var sample = document.createElement('canvas');
      var sw = Math.min(400, w);
      var sh = Math.round(sw * (h / w));
      sample.width = sw;
      sample.height = sh;
      var sctx = sample.getContext('2d');
      sctx.drawImage(img, 0, 0, sw, sh);
      var data = sctx.getImageData(0, 0, sw, sh).data;

      var minX = sw, minY = sh, maxX = 0, maxY = 0;
      var found = false;
      for (var y = 0; y < sh; y++) {
        for (var x = 0; x < sw; x++) {
          var i = (y * sw + x) * 4;
          var lum = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
          if (lum < 235) {
            found = true;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
          }
        }
      }

      if (!found || maxX - minX < sw * 0.15 || maxY - minY < sh * 0.15) {
        cb(null);
        return;
      }

      var pad = Math.round(Math.min(sw, sh) * 0.04);
      minX = Math.max(0, minX - pad);
      minY = Math.max(0, minY - pad);
      maxX = Math.min(sw - 1, maxX + pad);
      maxY = Math.min(sh - 1, maxY + pad);

      var scaleX = w / sw;
      var scaleY = h / sh;
      var cw = Math.round((maxX - minX) * scaleX);
      var ch = Math.round((maxY - minY) * scaleY);
      var c = document.createElement('canvas');
      c.width = cw;
      c.height = ch;
      c.getContext('2d').drawImage(
        img,
        Math.round(minX * scaleX), Math.round(minY * scaleY), cw, ch,
        0, 0, cw, ch
      );
      cb(c.toDataURL('image/jpeg', 0.92));
    };
    img.onerror = function () { cb(null); };
    img.src = dataUrl;
  }

  return {
    start: start, stop: stop, capture: capture,
    dataUrlToBlob: dataUrlToBlob, rotateImage: rotateImage, autoCrop: autoCrop,
    getRotation: function () { return rotation; },
    setRotation: function (r) { rotation = r; }
  };
})();

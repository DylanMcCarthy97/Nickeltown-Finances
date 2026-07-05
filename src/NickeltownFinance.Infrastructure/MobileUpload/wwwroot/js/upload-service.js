window.NFUpload = (function () {
  'use strict';

  var MAX_BYTES = 25 * 1024 * 1024;
  var ALLOWED_EXT = ['.pdf', '.jpg', '.jpeg', '.png', '.webp', '.heic', '.tif', '.tiff'];

  var STAGES = [
    { id: 'uploaded', label: 'Uploaded', icon: '🟢' },
    { id: 'processing', label: 'Processing', icon: '🟡' },
    { id: 'ocr', label: 'OCR', icon: '🟡' },
    { id: 'matching', label: 'Matching', icon: '🟡' },
    { id: 'completed', label: 'Completed', icon: '🟢' }
  ];

  var TRANSACTION_STAGES = [
    { id: 'uploaded', label: 'Uploaded', icon: '🟢' },
    { id: 'processing', label: 'Desktop processing', icon: '🟡' },
    { id: 'ocr', label: 'OCR', icon: '🟡' },
    { id: 'attached', label: 'Attached', icon: '🟢' }
  ];

  function getExtension(name) {
    var i = name.lastIndexOf('.');
    return i >= 0 ? name.substring(i).toLowerCase() : '';
  }

  function validateFile(file, fileName) {
    if (!file) return 'No file selected.';
    if (file.size > MAX_BYTES) return 'The file is too large.';
    var name = fileName || file.name || '';
    if (ALLOWED_EXT.indexOf(getExtension(name)) < 0) return 'This file type isn\'t supported.';
    return null;
  }

  function isTruthyFlag(value) {
    return value === true || value === 'true' || value === 1;
  }

  function normalizeUploadData(raw) {
    if (!raw || typeof raw !== 'object') return {};
    return {
      uploadId: raw.uploadId || raw.UploadId || null,
      importItemId: raw.importItemId || raw.ImportItemId || null,
      success: raw.success !== undefined ? raw.success : raw.Success,
      uploadSucceeded: raw.uploadSucceeded !== undefined ? raw.uploadSucceeded : raw.UploadSucceeded,
      status: raw.status || raw.Status || null,
      target: raw.target || raw.Target || null,
      message: raw.message || raw.Message || null,
      error: raw.error || raw.Error || null
    };
  }

  async function readJsonBody(res) {
    var text = await res.text();
    if (!text || !text.trim()) {
      console.warn('[upload] empty response body HTTP', res.status);
      return {};
    }
    try {
      return JSON.parse(text);
    } catch (e) {
      console.warn('[upload] JSON parse failed HTTP', res.status, text.slice(0, 240));
      return {};
    }
  }

  function isUploadHttpSuccess(res, raw) {
    if (!res.ok) return false;
    var data = normalizeUploadData(raw);
    if (data.error && !isTruthyFlag(data.success) && !isTruthyFlag(data.uploadSucceeded)) return false;
    if (data.uploadId || data.importItemId) return true;
    if (isTruthyFlag(data.success) || isTruthyFlag(data.uploadSucceeded)) return true;
    if (String(data.status || '').toLowerCase() === 'uploaded') return true;
    return false;
  }

  function extractUploadId(raw) {
    var data = normalizeUploadData(raw);
    return data.importItemId || data.uploadId || null;
  }

  function isNetworkError(err) {
    return err instanceof TypeError || (err && err.message && err.message.indexOf('fetch') >= 0);
  }

  function mapHttpError(status, data) {
    var serverMsg = data && (data.error || data.message);
    switch (status) {
      case 401:
        return 'Your upload session has expired. Please scan a new QR code.';
      case 403:
        return 'Your phone is not on the same network as the desktop.';
      case 413:
        return 'The file is too large.';
      case 415:
        return 'This file type isn\'t supported.';
      case 429:
        return serverMsg || 'Upload limit reached. Refresh the QR code on the desktop app.';
      case 500:
      case 502:
      case 503:
        return 'The desktop app encountered an unexpected error.';
      case 400:
        return serverMsg || 'The upload request was invalid.';
      default:
        if (serverMsg) return serverMsg;
        if (status >= 500) return 'The desktop app encountered an unexpected error.';
        return 'Could not upload the receipt (HTTP ' + status + ').';
    }
  }

  function mapUploadError(error, context) {
    if (!error) {
      if (context === 'network') return 'Could not reach the desktop app. Check Wi‑Fi.';
      return 'Could not upload the receipt. Check your connection and try again.';
    }
    var lower = String(error).toLowerCase();
    if (lower.indexOf('expired') >= 0 || lower.indexOf('invalid session') >= 0) {
      return 'Your upload session has expired. Please scan a new QR code.';
    }
    if (lower.indexOf('wi-fi') >= 0 || lower.indexOf('local network') >= 0 || lower.indexOf('same network') >= 0) {
      return 'Your phone is not on the same network as the desktop.';
    }
    if (lower.indexOf('not reachable') >= 0 || lower.indexOf('not active') >= 0) {
      return 'Desktop app not reachable.';
    }
    if (lower.indexOf('too large') >= 0) return 'The file is too large.';
    if (lower.indexOf('unsupported') >= 0 || lower.indexOf('file type') >= 0) {
      return 'This file type isn\'t supported.';
    }
    if (lower.indexOf('unexpected error') >= 0) {
      return 'The desktop app encountered an unexpected error.';
    }
    if (lower.indexOf('upload failed') >= 0 && context === 'ambiguous') {
      return 'Upload may have succeeded — check the desktop app.';
    }
    return error;
  }

  function mapStageToTimeline(stage) {
    var lower = String(stage || '').toLowerCase();
    if (lower.indexOf('fail') >= 0) return 'failed';
    if (lower.indexOf('committed') >= 0 || lower.indexOf('attached') >= 0) return 'attached';
    if (lower.indexOf('ready') >= 0 || lower.indexOf('completed') >= 0 || lower.indexOf('warning') >= 0) return 'completed';
    if (lower.indexOf('ocr') >= 0) return 'ocr';
    if (lower.indexOf('match') >= 0) return 'matching';
    if (lower.indexOf('upload') >= 0 || lower.indexOf('queued') >= 0) return 'uploaded';
    return 'processing';
  }

  function registerQueueSync() {
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.ready.then(function (reg) {
        if (reg.sync) reg.sync.register('receipt-upload-sync').catch(function () {});
      });
    }
  }

  function compressImage(blob, fileName, settings) {
    return new Promise(function (resolve) {
      if (!blob.type || blob.type.indexOf('image/') !== 0 || settings.quality === 'original') {
        resolve(blob);
        return;
      }

      var img = new Image();
      var url = URL.createObjectURL(blob);
      img.onload = function () {
        URL.revokeObjectURL(url);
        var maxDim = NFSettings.maxDimension(settings.quality);
        var w = img.width;
        var h = img.height;
        var scale = Math.min(1, maxDim / Math.max(w, h));
        var cw = Math.round(w * scale);
        var ch = Math.round(h * scale);
        var canvas = document.createElement('canvas');
        canvas.width = cw;
        canvas.height = ch;
        canvas.getContext('2d').drawImage(img, 0, 0, cw, ch);
        var q = NFSettings.qualityFactor(settings.quality);
        canvas.toBlob(function (b) {
          if (!b) { resolve(blob); return; }
          var outName = fileName.replace(/\.[^.]+$/, '.jpg');
          resolve(new File([b], outName, { type: 'image/jpeg' }));
        }, 'image/jpeg', q);
      };
      img.onerror = function () {
        URL.revokeObjectURL(url);
        resolve(blob);
      };
      img.src = url;
    });
  }

  async function uploadFile(blob, fileName, thumbDataUrl) {
    var settings = NFSettings.load();
    var err = validateFile(blob, fileName);
    if (err) {
      console.warn('[upload] validation failed:', err);
      return { ok: false, uploadFailed: true, error: err, type: 'validation' };
    }

    if (NFConnection.getState() !== 'connected') {
      console.warn('[upload] desktop not connected — queuing locally');
      await NFQueue.enqueue({
        blob: await blob.arrayBuffer(),
        fileName: fileName,
        contentType: blob.type || 'application/octet-stream',
        thumbDataUrl: thumbDataUrl || null,
        queuedAt: new Date().toISOString()
      });
      registerQueueSync();
      NFHistory.add({
        importItemId: 'queued-' + Date.now(),
        fileName: fileName,
        thumbDataUrl: thumbDataUrl,
        status: 'Waiting for desktop',
        uploadFailed: false,
        processingFailed: false,
        processingComplete: false,
        queued: true,
        uploadedAt: new Date().toISOString()
      });
      return { ok: false, queued: true, error: 'Queued — will upload when desktop is connected.', type: 'queued' };
    }

    console.log('[upload] compressing', fileName);
    var prepared = await compressImage(blob, fileName, settings);

    try {
      var form = new FormData();
      form.append('file', prepared, prepared.name || fileName);

      console.log('[upload] POST /api/upload', fileName, prepared.size || blob.size, 'bytes');
      var res = await fetch('/api/upload', {
        method: 'POST',
        headers: NFConnection.authHeaders(),
        body: form
      });

      var raw = await readJsonBody(res);
      var data = normalizeUploadData(raw);
      console.log('[upload] HTTP', res.status, raw);

      if (isUploadHttpSuccess(res, raw)) {
        var uploadId = extractUploadId(raw);
        console.log('[upload] success', uploadId);
        return {
          ok: true,
          importItemId: uploadId,
          target: data.target || NFConnection.getImportTarget(),
          message: data.message || 'Receipt uploaded',
          type: 'success'
        };
      }

      if (res.ok) {
        var maybeId = extractUploadId(raw);
        console.warn('[upload] HTTP', res.status, 'accepted but response shape unexpected', raw);
        return {
          ok: false,
          uploadFailed: false,
          ambiguous: true,
          importItemId: maybeId,
          target: data.target || NFConnection.getImportTarget(),
          error: maybeId
            ? 'Receipt uploaded — waiting for desktop processing.'
            : 'Receipt may have uploaded — check the desktop app.',
          type: 'ambiguous'
        };
      }

      var httpError = mapHttpError(res.status, data);
      console.warn('[upload] rejected HTTP', res.status, httpError);

      if (res.status >= 500 && extractUploadId(raw)) {
        return {
          ok: false,
          uploadFailed: false,
          ambiguous: true,
          importItemId: extractUploadId(raw),
          error: mapUploadError(httpError, 'ambiguous'),
          type: 'ambiguous'
        };
      }

      return {
        ok: false,
        uploadFailed: true,
        error: httpError,
        httpStatus: res.status,
        type: 'upload_failed'
      };
    } catch (e) {
      console.error('[upload] fetch error', e);
      if (isNetworkError(e)) {
        await NFQueue.enqueue({
          blob: await blob.arrayBuffer(),
          fileName: fileName,
          contentType: blob.type || 'application/octet-stream',
          thumbDataUrl: thumbDataUrl || null,
          queuedAt: new Date().toISOString()
        });
        registerQueueSync();
        return { ok: false, queued: true, error: 'Network error — receipt queued locally.', type: 'queued' };
      }
      return {
        ok: false,
        uploadFailed: true,
        error: mapUploadError(null, 'network'),
        type: 'upload_failed'
      };
    }
  }

  async function pollStatus(importItemId, onUpdate) {
    var res = await fetch('/api/status/' + encodeURIComponent(importItemId), {
      headers: NFConnection.authHeaders()
    });
    var data = await res.json().catch(function () { return null; });
    if (!data || data.error) return null;
    if (onUpdate) onUpdate(data);
    return data;
  }

  async function retryProcessing(importItemId) {
    var res = await fetch('/api/retry/' + encodeURIComponent(importItemId), {
      method: 'POST',
      headers: NFConnection.authHeaders()
    });
    var data = await res.json().catch(function () { return {}; });
    return { ok: res.ok && data.success, data: data };
  }

  async function flushQueue() {
    if (NFConnection.getState() !== 'connected') return 0;
    var items = await NFQueue.getAll();
    var done = 0;
    for (var i = 0; i < items.length; i++) {
      var item = items[i];
      var blob = new Blob([item.blob], { type: item.contentType });
      var result = await uploadFile(blob, item.fileName, item.thumbDataUrl);
      if (result.ok) {
        await NFQueue.remove(item.id);
        done++;
      }
    }
    return done;
  }

  return {
    STAGES: STAGES,
    TRANSACTION_STAGES: TRANSACTION_STAGES,
    validateFile: validateFile,
    uploadFile: uploadFile,
    pollStatus: pollStatus,
    retryProcessing: retryProcessing,
    flushQueue: flushQueue,
    mapStageToTimeline: mapStageToTimeline,
    compressImage: compressImage,
    mapHttpError: mapHttpError
  };
})();

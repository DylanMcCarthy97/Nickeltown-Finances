window.NFHistory = (function () {
  'use strict';

  var KEY = 'nf-receipt-history-v4';
  var MAX = 30;

  function load() {
    try { return JSON.parse(localStorage.getItem(KEY) || '[]'); }
    catch (e) { return []; }
  }

  function save(items) {
    localStorage.setItem(KEY, JSON.stringify(items.slice(0, MAX)));
  }

  function upsert(entry) {
    var items = load();
    var idx = items.findIndex(function (i) { return i.importItemId === entry.importItemId; });
    if (idx >= 0) items[idx] = Object.assign({}, items[idx], entry);
    else items.unshift(entry);
    save(items);
    return items;
  }

  function add(entry) { return upsert(entry); }

  function update(importItemId, patch) {
    var items = load();
    var idx = items.findIndex(function (i) { return i.importItemId === importItemId; });
    if (idx < 0) return items;
    items[idx] = Object.assign({}, items[idx], patch);
    save(items);
    return items;
  }

  function formatAmount(amount, currency) {
    if (amount == null || amount === '') return '';
    var n = Number(amount);
    if (isNaN(n)) return '';
    try {
      return new Intl.NumberFormat(undefined, { style: 'currency', currency: currency || 'AUD' }).format(n);
    } catch (e) {
      return '$' + n.toFixed(2);
    }
  }

  function statusKind(item) {
    if (item.queued) return 'queued';
    if (item.uploadFailed) return 'upload_failed';
    if (item.processingFailed) return 'processing_failed';
    if (item.processingComplete) return 'completed';
    var status = String(item.status || '').toLowerCase();
    if (status.indexOf('ocr') >= 0) return 'ocr';
    if (status.indexOf('match') >= 0) return 'processing';
    if (status.indexOf('attach') >= 0) return 'attached';
    if (status.indexOf('ready') >= 0 || status.indexOf('complete') >= 0) return 'ready';
    if (item.importItemId && !item.processingComplete) return 'processing';
    return 'uploaded';
  }

  function badgeClass(kind) {
    switch (kind) {
      case 'ready':
      case 'completed':
      case 'attached': return 'badge-ready';
      case 'processing': return 'badge-processing';
      case 'uploaded': return 'badge-uploaded';
      case 'ocr': return 'badge-ocr';
      case 'queued': return 'badge-queued';
      case 'upload_failed':
      case 'processing_failed': return 'badge-failed';
      default: return 'badge-uploaded';
    }
  }

  function statusLabel(kind, item) {
    if (kind === 'upload_failed' && item && item.status) return item.status;
    switch (kind) {
      case 'ready': return 'Ready';
      case 'completed': return 'Completed';
      case 'attached': return 'Attached';
      case 'processing': return 'Processing';
      case 'ocr': return 'OCR';
      case 'uploaded': return 'Uploaded';
      case 'queued': return 'Queued';
      case 'processing_failed': return 'Failed';
      case 'upload_failed': return 'Upload failed';
      default: return 'Uploaded';
    }
  }

  function renderBadge(kind, item) {
    var label = statusLabel(kind, item);
    var cls = badgeClass(kind);
    return '<span class="badge ' + cls + '"><span class="badge-dot" aria-hidden="true"></span>' + escapeHtml(label) + '</span>';
  }

  function formatDate(iso) {
    if (!iso) return '';
    try {
      return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
    } catch (e) {
      return '';
    }
  }

  function formatShortDate(iso) {
    if (!iso) return '';
    try {
      return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    } catch (e) {
      return '';
    }
  }

  function escapeHtml(text) {
    var d = document.createElement('div');
    d.textContent = text == null ? '' : String(text);
    return d.innerHTML;
  }

  function makeThumb(dataUrl, maxW, maxH, cb) {
    if (!dataUrl) { cb(null); return; }
    var img = new Image();
    img.onload = function () {
      var w = img.width;
      var h = img.height;
      var scale = Math.min(1, maxW / w, maxH / h);
      var c = document.createElement('canvas');
      c.width = Math.round(w * scale);
      c.height = Math.round(h * scale);
      c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
      cb(c.toDataURL('image/jpeg', 0.7));
    };
    img.onerror = function () { cb(null); };
    img.src = dataUrl;
  }

  return {
    load: load, save: save, add: add, update: update, upsert: upsert,
    formatAmount: formatAmount, statusKind: statusKind, badgeClass: badgeClass,
    statusLabel: statusLabel, renderBadge: renderBadge,
    formatDate: formatDate, formatShortDate: formatShortDate,
    escapeHtml: escapeHtml, makeThumb: makeThumb
  };
})();

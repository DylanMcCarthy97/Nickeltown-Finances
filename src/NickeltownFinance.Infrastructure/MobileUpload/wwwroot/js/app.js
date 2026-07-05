(function () {
  'use strict';

  var APP_VERSION = '1.3.1';
  var token = new URLSearchParams(window.location.search).get('token') || '';

  var screens = {};
  var currentScreen = 'home';
  var currentImportItemId = null;
  var currentFileName = '';
  var reviewDataUrl = null;
  var reviewRotation = 0;
  var pollTimer = null;
  var timeTimer = null;
  var deferredInstall = null;
  var isTransactionMode = false;
  var importTargetLabel = 'Receipt Inbox';

  var els = {};

  function $(id) { return document.getElementById(id); }

  function haptic() {
    var s = NFSettings.load();
    if (s.haptics && navigator.vibrate) navigator.vibrate(10);
  }

  function toast(message, type, duration) {
    var host = els.toastHost;
    var t = document.createElement('div');
    t.className = 'toast toast-' + (type || 'info');
    t.textContent = message;
    host.appendChild(t);
    setTimeout(function () {
      t.style.opacity = '0';
      t.style.transform = 'translateY(-8px)';
      setTimeout(function () { t.remove(); }, 300);
    }, duration || 3500);
  }

  function showScreen(name) {
    Object.keys(screens).forEach(function (k) {
      screens[k].classList.add('hidden');
      screens[k].classList.remove('screen-active');
    });
    screens[name].classList.remove('hidden');
    screens[name].classList.add('screen-active');
    currentScreen = name;
    updateNav();
    els.tabbar.classList.toggle('hidden', name === 'camera');
    els.sessionBar.classList.toggle('hidden', name === 'camera');
  }

  function updateNav() {
    ['home', 'history', 'settings'].forEach(function (tab) {
      var btn = els['nav' + tab.charAt(0).toUpperCase() + tab.slice(1)];
      if (!btn) return;
      var active = (tab === 'home' && (currentScreen === 'home' || currentScreen === 'review' || currentScreen === 'processing'))
        || currentScreen === tab;
      btn.classList.toggle('tab-active', active);
      if (active) btn.setAttribute('aria-current', 'page');
      else btn.removeAttribute('aria-current');
    });
  }

  function applyTheme() {
    var settings = NFSettings.load();
    var resolved = NFSettings.resolveTheme(settings);
    document.documentElement.setAttribute('data-theme', resolved);
    if (els.themeColorMeta) {
      els.themeColorMeta.content = resolved === 'dark' ? '#0A0C10' : '#F2F4F8';
    }
  }

  function updateClock() {
    if (!els.currentTime) return;
    els.currentTime.textContent = new Date().toLocaleTimeString(undefined, {
      hour: 'numeric', minute: '2-digit', hour12: true
    });
  }

  function sessionContextLabel() {
    return isTransactionMode ? 'Expense attachment' : 'Receipt Inbox';
  }

  function updateConnectionUI(info) {
    var badge = els.connectionBadge;
    badge.className = 'conn-badge conn-' + info.state;
    els.connectionLabel.textContent = NFConnection.labelFor(info.state);
    els.sessionCountdown.textContent = NFConnection.countdownText();
    if (els.sessionContext) els.sessionContext.textContent = sessionContextLabel();
    updateDiagnostics();
    updateErrorBanner(info.state);

    if (info.state === 'connected') {
      NFUpload.flushQueue().then(function (n) {
        if (n > 0) {
          toast(n + ' queued receipt' + (n > 1 ? 's' : '') + ' uploaded', 'success');
          renderHomeRecent();
          updateQueueBanner();
        }
      });
    }
  }

  function updateErrorBanner(state) {
    if (!els.errorBanner) return;
    var html = '';
    if (state === 'offline') {
      html = buildErrorCard('offline', 'Desktop offline', 'Your phone cannot reach Nickeltown Finances on this network. Make sure you are on the same Wi‑Fi as your desktop.', null);
    } else if (state === 'expired') {
      html = buildErrorCard('expired', 'Session expired', 'Scan a new QR code from the desktop app to continue uploading receipts.', null);
    } else if (!token) {
      html = buildErrorCard('expired', 'Not connected', 'Scan the QR code on your desktop to start a secure upload session.', null);
    }
    els.errorBanner.innerHTML = html;
    els.errorBanner.classList.toggle('hidden', !html);
  }

  function buildErrorCard(type, title, message, retryFn) {
    var cls = 'error-card error-card-' + type;
    var retry = retryFn ? '<button type="button" class="btn btn-primary btn-full error-retry">Retry</button>' : '';
    return '<div class="' + cls + '"><div class="error-card-icon">' + errorIcon(type) + '</div>' +
      '<h3>' + NFHistory.escapeHtml(title) + '</h3><p>' + NFHistory.escapeHtml(message) + '</p>' +
      (retry ? '<div class="error-actions">' + retry + '</div>' : '') + '</div>';
  }

  function errorIcon(type) {
    switch (type) {
      case 'wifi': return '📶';
      case 'offline': return '🖥';
      case 'expired': return '🔑';
      case 'upload': return '📤';
      default: return '⚠️';
    }
  }

  function showUploadError(result) {
    var type = 'upload';
    var title = 'Upload failed';
    var msg = result.error || 'Could not upload the receipt.';
    if (result.httpStatus === 403 || (msg && msg.indexOf('network') >= 0)) {
      type = 'wifi';
      title = 'Wrong Wi‑Fi';
    } else if (result.httpStatus === 401 || (msg && msg.indexOf('expired') >= 0)) {
      type = 'expired';
      title = 'Session expired';
    }
    toast(msg, 'error', 5000);
    if (els.errorBanner) {
      els.errorBanner.innerHTML = buildErrorCard(type, title, msg, true);
      els.errorBanner.classList.remove('hidden');
      var btn = els.errorBanner.querySelector('.error-retry');
      if (btn) btn.addEventListener('click', function () { haptic(); NFConnection.checkSession(); });
    }
  }

  function updateQueueBanner() {
    NFQueue.count().then(function (n) {
      els.queueBanner.classList.toggle('hidden', n === 0);
      els.queueCount.textContent = String(n);
    });
  }

  function updateDiagnostics() {
    var state = NFConnection.getState();
    var sync = NFConnection.getLastSync();
    els.diagStatus.textContent = NFConnection.labelFor(state);
    els.diagLastSync.textContent = sync ? sync.toLocaleTimeString() : '—';
    els.diagSession.textContent = NFConnection.countdownText();
    if (els.diagTarget) els.diagTarget.textContent = sessionContextLabel();
  }

  function getStages() {
    return isTransactionMode ? NFUpload.TRANSACTION_STAGES : NFUpload.STAGES;
  }

  function stageRingContent(card, stage, failed) {
    if (card.classList.contains('done')) return '✓';
    if (card.classList.contains('failed')) return '✕';
    if (card.classList.contains('active')) return '<span class="stage-spinner" aria-hidden="true"></span>';
    return '○';
  }

  function renderTimeline(activeStage, failed) {
    var timeline = els.timeline;
    timeline.innerHTML = '';
    var stages = getStages();
    var stageIdx = isTransactionMode
      ? { uploaded: 0, processing: 1, ocr: 2, attached: 3, completed: 3, failed: 3 }
      : { uploaded: 0, processing: 1, ocr: 2, matching: 3, completed: 4, failed: 4 };
    var current = failed ? 'failed' : activeStage;

    stages.forEach(function (stage, i) {
      var card = document.createElement('div');
      card.className = 'stage-card';
      card.setAttribute('role', 'listitem');
      var ci = stageIdx[current] != null ? stageIdx[current] : 1;
      if (i < ci) card.classList.add('done');
      else if (i === ci) card.classList.add(failed && (stage.id === 'completed' || stage.id === 'attached') ? 'failed' : 'active');
      else card.classList.add('pending');

      var kind = 'processing';
      if (card.classList.contains('done')) kind = stage.id === 'ocr' ? 'ocr' : (stage.id === 'attached' ? 'attached' : 'ready');
      if (card.classList.contains('active')) {
        if (stage.id === 'uploaded') kind = 'uploaded';
        else if (stage.id === 'ocr') kind = 'ocr';
        else kind = 'processing';
      }
      if (card.classList.contains('failed')) kind = 'processing_failed';

      var label = stage.label;
      if (failed && (stage.id === 'attached' || stage.id === 'completed')) label = 'Processing failed';

      card.innerHTML =
        '<span class="stage-ring">' + stageRingContent(card, stage, failed) + '</span>' +
        '<span class="stage-label">' + NFHistory.escapeHtml(label) + '</span>' +
        NFHistory.renderBadge(kind, null);

      timeline.appendChild(card);
    });
  }

  function showUploadedState() {
    els.uploadSuccessHero.classList.remove('hidden');
    els.uploadProgress.classList.add('hidden');
    if (els.processingSubtitle) {
      els.processingSubtitle.textContent = isTransactionMode
        ? 'Attaching to your expense on desktop'
        : 'Processing on your desktop now';
    }
    renderTimeline('uploaded', false);
  }

  function stopPolling() {
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
  }

  function startPolling(importItemId, fileName, thumbDataUrl, target) {
    stopPolling();
    currentImportItemId = importItemId;
    currentFileName = fileName;
    isTransactionMode = target === 'Transaction' || NFConnection.isTransactionSession();
    showScreen('processing');
    els.processingFailedCard.classList.add('hidden');
    els.btnDoneProcessing.classList.add('hidden');
    showUploadedState();

    pollTimer = setInterval(async function () {
      try {
        var data = await NFUpload.pollStatus(importItemId);
        if (!data) return;

        var stage = NFUpload.mapStageToTimeline(data.stage || data.status);
        if (data.isAttached) stage = 'attached';
        renderTimeline(stage, data.processingFailed);

        NFHistory.update(importItemId, {
          status: data.statusDisplay || data.stage,
          supplier: data.supplier,
          amount: data.amount,
          currency: data.currency,
          processingComplete: data.processingComplete,
          processingFailed: data.processingFailed,
          uploadFailed: false,
          errorMessage: data.errorMessage
        });

        if (data.processingComplete) {
          stopPolling();
          if (data.processingFailed) {
            els.processingFailedCard.classList.remove('hidden');
            els.processingFailedMsg.textContent = data.statusDisplay || 'Receipt uploaded successfully. Desktop processing failed.';
            if (data.errorMessage) els.processingFailedMsg.textContent += ' ' + data.errorMessage;
            toast('Processing failed — upload was successful', 'warning', 5000);
          } else if (data.isAttached || isTransactionMode) {
            toast('Receipt attached successfully', 'success');
            if (els.uploadSuccessHero.querySelector('h2')) {
              els.uploadSuccessHero.querySelector('h2').textContent = 'Receipt attached';
            }
            if (els.processingSubtitle) {
              els.processingSubtitle.textContent = 'Attached to your expense on desktop';
            }
            renderTimeline('attached', false);
            showOverlaySuccess();
            setTimeout(function () { showScreen('home'); renderHomeRecent(); }, 2000);
          } else {
            toast('Receipt processed on desktop', 'success');
            showOverlaySuccess();
            setTimeout(function () { showScreen('home'); renderHomeRecent(); }, 2000);
          }
          renderHistory();
          renderHomeRecent();
        }
      } catch (e) { /* keep polling */ }
    }, 1200);
  }

  function showOverlaySuccess() {
    els.overlaySuccess.classList.remove('hidden');
    haptic();
    setTimeout(function () { els.overlaySuccess.classList.add('hidden'); }, 1400);
  }

  async function handleUpload(blob, fileName, thumbDataUrl) {
    isTransactionMode = NFConnection.isTransactionSession();
    showScreen('processing');
    els.uploadSuccessHero.classList.add('hidden');
    els.processingFailedCard.classList.add('hidden');
    els.uploadProgress.classList.remove('hidden');
    els.timeline.innerHTML = '';

    try {
      var result = await NFUpload.uploadFile(blob, fileName, thumbDataUrl);

      if (result.ok) {
        haptic();
        isTransactionMode = result.target === 'Transaction' || NFConnection.isTransactionSession();
        showUploadedState();
        showOverlaySuccess();

        NFHistory.add({
          importItemId: result.importItemId,
          fileName: fileName,
          thumbDataUrl: thumbDataUrl,
          status: 'Receipt uploaded',
          uploadFailed: false,
          processingFailed: false,
          processingComplete: false,
          uploadedAt: new Date().toISOString()
        });

        toast(isTransactionMode ? 'Receipt uploaded — attaching on desktop' : 'Receipt uploaded', 'success');
        startPolling(result.importItemId, fileName, thumbDataUrl, result.target);
        renderHomeRecent();
        return;
      }

      if (result.queued) {
        toast(result.error, 'warning', 5000);
        showScreen('home');
        updateQueueBanner();
        renderHomeRecent();
        return;
      }

      if (result.ambiguous) {
        toast(result.error, 'warning', 6000);
        if (result.importItemId) {
          NFHistory.add({
            importItemId: result.importItemId,
            fileName: fileName,
            thumbDataUrl: thumbDataUrl,
            status: 'Receipt uploaded',
            uploadFailed: false,
            processingFailed: false,
            processingComplete: false,
            uploadedAt: new Date().toISOString()
          });
          startPolling(result.importItemId, fileName, thumbDataUrl, result.target);
        } else {
          showScreen('home');
        }
        renderHomeRecent();
        return;
      }

      showUploadError(result);
      NFHistory.add({
        importItemId: 'failed-' + Date.now(),
        fileName: fileName,
        thumbDataUrl: thumbDataUrl,
        status: result.error,
        uploadFailed: true,
        processingFailed: false,
        processingComplete: false,
        uploadedAt: new Date().toISOString()
      });
      showScreen('home');
      renderHomeRecent();
    } catch (e) {
      console.error('[upload] handleUpload error', e);
      showUploadError({ error: 'Could not reach the desktop app. Check Wi‑Fi.' });
      showScreen('home');
    }
  }

  function getExtension(name) {
    var i = (name || '').lastIndexOf('.');
    return i >= 0 ? name.substring(i).toLowerCase() : '';
  }

  function thumbHtml(item) {
    if (item.thumbDataUrl) {
      return '<img class="receipt-card-thumb" src="' + item.thumbDataUrl + '" alt="" loading="lazy" />';
    }
    var icon = getExtension(item.fileName) === '.pdf' ? '📄' : '🧾';
    return '<div class="receipt-card-thumb-placeholder" aria-hidden="true">' + icon + '</div>';
  }

  function openDetailSheet(item) {
    var kind = NFHistory.statusKind(item);
    els.sheetTitle.textContent = item.supplier || item.fileName || 'Receipt';
    var amount = NFHistory.formatAmount(item.amount, item.currency);
    els.sheetAmount.textContent = amount || '';
    els.sheetAmount.classList.toggle('hidden', !amount);
    els.sheetBadge.innerHTML = NFHistory.renderBadge(kind, item);
    var lines = [];
    lines.push('Uploaded ' + NFHistory.formatDate(item.uploadedAt));
    if (item.status) lines.push(item.status);
    if (item.errorMessage) lines.push(item.errorMessage);
    lines.push(item.fileName);
    els.sheetMeta.textContent = lines.filter(Boolean).join('\n');
    els.sheetMeta.style.whiteSpace = 'pre-line';

    if (item.thumbDataUrl) {
      els.sheetThumb.src = item.thumbDataUrl;
      els.sheetThumb.classList.remove('hidden');
    } else {
      els.sheetThumb.classList.add('hidden');
    }

    els.sheetBackdrop.classList.remove('hidden');
    els.detailSheet.classList.remove('hidden');
    haptic();
  }

  function closeDetailSheet() {
    els.sheetBackdrop.classList.add('hidden');
    els.detailSheet.classList.add('hidden');
  }

  function renderHomeRecent() {
    var items = NFHistory.load().slice(0, 8);
    var strip = els.homeRecent;
    var empty = els.homeRecentEmpty;
    var seeAll = els.btnSeeAll;

    strip.innerHTML = '';
    if (!items.length) {
      empty.classList.remove('hidden');
      seeAll.classList.add('hidden');
      return;
    }
    empty.classList.add('hidden');
    seeAll.classList.remove('hidden');

    items.forEach(function (item, i) {
      var kind = NFHistory.statusKind(item);
      var card = document.createElement('button');
      card.type = 'button';
      card.className = 'receipt-card';
      card.style.animationDelay = (i * 0.05) + 's';
      card.setAttribute('role', 'listitem');
      card.setAttribute('aria-label', (item.supplier || item.fileName) + ', ' + NFHistory.statusLabel(kind, item));

      var amount = NFHistory.formatAmount(item.amount, item.currency);
      card.innerHTML =
        thumbHtml(item) +
        '<div class="receipt-card-body">' +
          '<div class="receipt-card-supplier">' + NFHistory.escapeHtml(item.supplier || item.fileName) + '</div>' +
          '<div class="receipt-card-meta">' +
            (amount ? '<span class="receipt-card-amount">' + NFHistory.escapeHtml(amount) + '</span>' : '<span></span>') +
            NFHistory.renderBadge(kind, item) +
          '</div>' +
          '<div class="receipt-card-date">' + NFHistory.escapeHtml(NFHistory.formatShortDate(item.uploadedAt)) + '</div>' +
        '</div>';

      card.addEventListener('click', function () { openDetailSheet(item); });
      strip.appendChild(card);
    });
  }

  function renderHistory() {
    var items = NFHistory.load();
    var list = els.historyList;
    var empty = els.historyEmpty;
    list.innerHTML = '';

    if (!items.length) {
      empty.classList.remove('hidden');
      return;
    }
    empty.classList.add('hidden');

    items.forEach(function (item, i) {
      var kind = NFHistory.statusKind(item);
      var card = document.createElement('div');
      card.className = 'history-card';
      card.style.animationDelay = (i * 0.04) + 's';

      var thumbHtmlHist = item.thumbDataUrl
        ? '<img class="history-thumb" src="' + item.thumbDataUrl + '" alt="" loading="lazy" />'
        : '<div class="history-thumb history-thumb-placeholder">' + (getExtension(item.fileName) === '.pdf' ? '📄' : '🧾') + '</div>';

      var amount = NFHistory.formatAmount(item.amount, item.currency);
      var time = NFHistory.formatDate(item.uploadedAt);

      card.innerHTML =
        '<div class="history-card-head" role="button" tabindex="0" aria-expanded="false">' +
          thumbHtmlHist +
          '<div class="history-info">' +
            '<div class="history-supplier">' + NFHistory.escapeHtml(item.supplier || item.fileName) + '</div>' +
            (amount ? '<div class="history-amount">' + NFHistory.escapeHtml(amount) + '</div>' : '') +
            '<div class="history-time">' + NFHistory.escapeHtml(time) + '</div>' +
          '</div>' +
          NFHistory.renderBadge(kind, item) +
          '<span class="history-chevron" aria-hidden="true">▼</span>' +
        '</div>' +
        '<div class="history-body hidden">' +
          '<div class="history-detail">' +
            '<div class="history-detail-row">' + NFHistory.escapeHtml(item.status || '') + '</div>' +
            (item.errorMessage ? '<div class="history-detail-row">' + NFHistory.escapeHtml(item.errorMessage) + '</div>' : '') +
            '<div class="history-detail-row muted">' + NFHistory.escapeHtml(item.fileName) + '</div>' +
          '</div>' +
        '</div>';

      var head = card.querySelector('.history-card-head');
      head.addEventListener('click', function (e) {
        if (e.target.closest('.badge')) {
          openDetailSheet(item);
          return;
        }
        haptic();
        var body = card.querySelector('.history-body');
        var expanded = !body.classList.contains('hidden');
        body.classList.toggle('hidden', expanded);
        card.classList.toggle('expanded', !expanded);
        head.setAttribute('aria-expanded', expanded ? 'false' : 'true');
      });

      list.appendChild(card);
    });
  }

  function openReview(dataUrl) {
    reviewDataUrl = dataUrl;
    reviewRotation = 0;
    els.reviewImage.src = dataUrl;
    els.reviewFrame.classList.remove('cropping');
    NFCamera.stop();
    showScreen('review');
  }

  async function openCamera() {
    showScreen('camera');
    var ok = await NFCamera.start(
      els.cameraVideo,
      document.querySelector('.camera-guide'),
      function (dataUrl) { openReview(dataUrl); }
    );
    if (!ok) {
      toast('Camera unavailable — choose from gallery instead', 'warning');
      showScreen('home');
    }
  }

  function bindSettings() {
    var settings = NFSettings.load();

    document.querySelectorAll('[data-theme]').forEach(function (btn) {
      btn.classList.toggle('seg-active', btn.getAttribute('data-theme') === settings.theme);
      btn.addEventListener('click', function () {
        haptic();
        var theme = btn.getAttribute('data-theme');
        NFSettings.save({ theme: theme });
        document.querySelectorAll('[data-theme]').forEach(function (b) {
          b.classList.toggle('seg-active', b === btn);
        });
        applyTheme();
      });
    });

    document.querySelectorAll('[data-quality]').forEach(function (btn) {
      btn.classList.toggle('seg-active', btn.getAttribute('data-quality') === settings.quality);
      btn.addEventListener('click', function () {
        haptic();
        var q = btn.getAttribute('data-quality');
        NFSettings.save({ quality: q });
        document.querySelectorAll('[data-quality]').forEach(function (b) {
          b.classList.toggle('seg-active', b === btn);
        });
      });
    });

    els.settingRearCamera.checked = settings.rearCamera;
    els.settingHaptics.checked = settings.haptics;

    els.settingRearCamera.addEventListener('change', function () {
      NFSettings.save({ rearCamera: els.settingRearCamera.checked });
    });
    els.settingHaptics.addEventListener('change', function () {
      NFSettings.save({ haptics: els.settingHaptics.checked });
    });

    els.btnDiagTest.addEventListener('click', async function () {
      haptic();
      toast('Testing connection…', 'info', 2000);
      var r = await NFConnection.checkSession();
      toast(r.valid ? 'Desktop connected' : (r.error || 'Desktop offline'), r.valid ? 'success' : 'error');
      updateDiagnostics();
    });
  }

  function bindEvents() {
    els.btnCamera.addEventListener('click', function () { haptic(); openCamera(); });
    els.btnGallery.addEventListener('click', function () { haptic(); els.inputGallery.click(); });
    els.btnPdf.addEventListener('click', function () { haptic(); els.inputPdf.click(); });

    els.inputGallery.addEventListener('change', function () {
      var file = els.inputGallery.files && els.inputGallery.files[0];
      els.inputGallery.value = '';
      if (!file) return;
      var reader = new FileReader();
      reader.onload = function () { openReview(reader.result); };
      reader.readAsDataURL(file);
    });

    els.inputPdf.addEventListener('change', async function () {
      var file = els.inputPdf.files && els.inputPdf.files[0];
      els.inputPdf.value = '';
      if (!file) return;
      await handleUpload(file, file.name, null);
    });

    els.cameraClose.addEventListener('click', function () {
      NFCamera.stop();
      showScreen('home');
    });

    els.cameraShutter.addEventListener('click', function () { NFCamera.capture(); });

    els.reviewBack.addEventListener('click', function () {
      reviewDataUrl = null;
      showScreen('home');
    });

    els.reviewRetake.addEventListener('click', function () {
      reviewDataUrl = null;
      openCamera();
    });

    els.reviewRotate.addEventListener('click', function () {
      if (!reviewDataUrl) return;
      haptic();
      reviewRotation = (reviewRotation + 90) % 360;
      NFCamera.rotateImage(reviewDataUrl, 90, function (url) {
        reviewDataUrl = url;
        els.reviewImage.src = url;
      });
    });

    els.reviewCrop.addEventListener('click', function () {
      if (!reviewDataUrl) return;
      haptic();
      els.reviewFrame.classList.toggle('cropping');
      if (!els.reviewFrame.classList.contains('cropping')) return;
      NFCamera.autoCrop(reviewDataUrl, function (url) {
        if (url) {
          reviewDataUrl = url;
          els.reviewImage.src = url;
          els.reviewFrame.classList.remove('cropping');
          toast('Receipt cropped', 'success', 2000);
        }
      });
    });

    els.reviewUpload.addEventListener('click', async function () {
      if (!reviewDataUrl) return;
      haptic();
      var blob = NFCamera.dataUrlToBlob(reviewDataUrl);
      var fileName = 'receipt-' + Date.now() + '.jpg';
      NFHistory.makeThumb(reviewDataUrl, 140, 100, async function (thumb) {
        await handleUpload(blob, fileName, thumb);
      });
    });

    els.btnRetry.addEventListener('click', async function () {
      if (!currentImportItemId) return;
      haptic();
      var r = await NFUpload.retryProcessing(currentImportItemId);
      if (!r.ok) { toast('Could not retry processing', 'error'); return; }
      toast('Retrying on desktop…', 'info');
      els.processingFailedCard.classList.add('hidden');
      startPolling(currentImportItemId, currentFileName);
    });

    els.btnDetails.addEventListener('click', function () {
      var item = NFHistory.load().find(function (i) { return i.importItemId === currentImportItemId; });
      if (item) openDetailSheet(item);
      else toast('Check the receipt inbox on your desktop for details', 'info', 5000);
    });

    els.btnDoneProcessing.addEventListener('click', function () { showScreen('home'); });

    els.navHome.addEventListener('click', function () {
      if (currentScreen === 'camera') NFCamera.stop();
      showScreen('home');
      haptic();
    });
    els.navHistory.addEventListener('click', function () {
      renderHistory();
      showScreen('history');
      haptic();
    });
    els.navSettings.addEventListener('click', function () {
      updateDiagnostics();
      showScreen('settings');
      haptic();
    });

    els.btnSeeAll.addEventListener('click', function () {
      renderHistory();
      showScreen('history');
    });

    els.sheetBackdrop.addEventListener('click', closeDetailSheet);
    els.sheetClose.addEventListener('click', closeDetailSheet);

    window.addEventListener('beforeinstallprompt', function (e) {
      e.preventDefault();
      deferredInstall = e;
      els.installBanner.classList.remove('hidden');
    });

    els.installAccept.addEventListener('click', async function () {
      if (!deferredInstall) return;
      deferredInstall.prompt();
      await deferredInstall.userChoice;
      deferredInstall = null;
      els.installBanner.classList.add('hidden');
    });

    els.installDismiss.addEventListener('click', function () {
      els.installBanner.classList.add('hidden');
    });

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function () {
      if (NFSettings.load().theme === 'auto') applyTheme();
    });

    window.addEventListener('online', function () {
      NFConnection.checkSession();
      NFUpload.flushQueue().then(function () { updateQueueBanner(); renderHomeRecent(); });
    });

    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.addEventListener('message', function (e) {
        if (e.data && e.data.type === 'FLUSH_QUEUE') {
          NFUpload.flushQueue().then(function () { updateQueueBanner(); renderHomeRecent(); });
        }
      });
    }
  }

  function cacheElements() {
    els = {
      splash: $('splash'), app: $('app'), toastHost: $('toast-host'), tabbar: document.querySelector('.tabbar'),
      sessionBar: $('session-bar'),
      connectionBadge: $('connection-badge'), connectionLabel: $('connection-label'),
      sessionCountdown: $('session-countdown'), sessionContext: $('session-context'),
      currentTime: $('current-time'), themeColorMeta: $('theme-color-meta'),
      queueBanner: $('queue-banner'), queueCount: $('queue-count'), errorBanner: $('error-banner'),
      homeRecent: $('home-recent'), homeRecentEmpty: $('home-recent-empty'), btnSeeAll: $('btn-see-all'),
      btnCamera: $('btn-camera'), btnGallery: $('btn-gallery'), btnPdf: $('btn-pdf'),
      inputGallery: $('input-gallery'), inputPdf: $('input-pdf'),
      cameraVideo: $('camera-video'), cameraClose: $('camera-close'), cameraShutter: $('camera-shutter'),
      reviewImage: $('review-image'), reviewFrame: $('review-frame'),
      reviewBack: $('review-back'), reviewRetake: $('review-retake'), reviewRotate: $('review-rotate'),
      reviewCrop: $('review-crop'), reviewUpload: $('review-upload'),
      timeline: $('timeline'), uploadSuccessHero: $('upload-success-hero'), uploadProgress: $('upload-progress'),
      processingSubtitle: $('processing-subtitle'),
      processingFailedCard: $('processing-failed-card'), processingFailedMsg: $('processing-failed-msg'),
      btnRetry: $('btn-retry'), btnDetails: $('btn-details'), btnDoneProcessing: $('btn-done-processing'),
      historyList: $('history-list'), historyEmpty: $('history-empty'),
      diagStatus: $('diag-status'), diagLastSync: $('diag-last-sync'), diagSession: $('diag-session'),
      diagTarget: $('diag-target'), btnDiagTest: $('btn-diag-test'),
      settingRearCamera: $('setting-rear-camera'), settingHaptics: $('setting-haptics'),
      navHome: $('nav-home'), navHistory: $('nav-history'), navSettings: $('nav-settings'),
      installBanner: $('install-banner'), installAccept: $('install-accept'), installDismiss: $('install-dismiss'),
      overlaySuccess: $('overlay-success'), appVersion: $('app-version'), settingsVersion: $('settings-version'),
      sheetBackdrop: $('sheet-backdrop'), detailSheet: $('detail-sheet'),
      sheetThumb: $('sheet-thumb'), sheetTitle: $('sheet-title'), sheetAmount: $('sheet-amount'),
      sheetBadge: $('sheet-badge'), sheetMeta: $('sheet-meta'), sheetClose: $('sheet-close')
    };

    screens = {
      home: $('screen-home'), camera: $('screen-camera'), review: $('screen-review'),
      processing: $('screen-processing'), history: $('screen-history'), settings: $('screen-settings')
    };
  }

  async function init() {
    cacheElements();
    els.appVersion.textContent = APP_VERSION;
    els.settingsVersion.textContent = APP_VERSION;

    applyTheme();
    bindSettings();
    bindEvents();
    updateClock();
    timeTimer = setInterval(updateClock, 1000);

    NFConnection.init(token);
    NFConnection.onChange(updateConnectionUI);

    if (!token) {
      toast('Scan the QR code on your desktop to connect', 'error', 8000);
      updateErrorBanner('offline');
    } else {
      var session = await NFConnection.checkSession();
      if (session.valid && session.data && session.data.importTarget) {
        isTransactionMode = session.data.importTarget === 'Transaction';
        importTargetLabel = sessionContextLabel();
      }
      NFConnection.startPolling(8000);
    }

    renderHomeRecent();
    updateQueueBanner();
    updateDiagnostics();

    setTimeout(function () {
      els.splash.classList.add('hide');
      els.app.classList.remove('hidden');
    }, 700);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

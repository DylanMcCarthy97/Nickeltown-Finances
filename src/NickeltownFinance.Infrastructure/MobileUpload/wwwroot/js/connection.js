window.NFConnection = (function () {
  'use strict';

  var token = '';
  var state = 'connecting';
  var lastSync = null;
  var sessionExpiresAt = null;
  var importTarget = 'Inbox';
  var listeners = [];
  var pollTimer = null;
  var countdownTimer = null;

  function onChange(fn) { listeners.push(fn); }

  function emit() {
    listeners.forEach(function (fn) {
      fn({ state: state, lastSync: lastSync, sessionExpiresAt: sessionExpiresAt, importTarget: importTarget });
    });
  }

  function setState(s) {
    state = s;
    emit();
  }

  function labelFor(state) {
    switch (state) {
      case 'connected': return 'Desktop connected';
      case 'connecting': return 'Connecting';
      case 'reconnecting': return 'Reconnecting';
      case 'offline': return 'Desktop offline';
      case 'expired': return 'Session expired';
      default: return 'Desktop offline';
    }
  }

  function init(sessionToken) {
    token = sessionToken || '';
  }

  async function checkSession() {
    if (!token) {
      setState('offline');
      return { valid: false, error: 'Missing session token' };
    }

    if (state === 'offline') setState('reconnecting');
    else if (state !== 'connected') setState('connecting');

    try {
      var res = await fetch('/api/session?token=' + encodeURIComponent(token), {
        headers: { 'Authorization': 'Bearer ' + token }
      });
      var data = await res.json().catch(function () { return {}; });

      if (res.ok && data.valid) {
        setState('connected');
        lastSync = new Date();
        if (data.expiresAt) {
          sessionExpiresAt = data.expiresAt;
          startCountdown();
        }
        if (data.importTarget) importTarget = data.importTarget;
        emit();
        return { valid: true, data: data };
      }

      var err = (data.error || '').toLowerCase();
      if (err.indexOf('expired') >= 0) setState('expired');
      else setState('offline');
      return { valid: false, error: data.error || 'Desktop not reachable' };
    } catch (e) {
      setState('offline');
      return { valid: false, error: 'Network error' };
    }
  }

  function startPolling(intervalMs) {
    stopPolling();
    checkSession();
    pollTimer = setInterval(checkSession, intervalMs || 8000);
  }

  function stopPolling() {
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
  }

  function startCountdown() {
    if (countdownTimer) clearInterval(countdownTimer);
    countdownTimer = setInterval(function () {
      if (!sessionExpiresAt) return;
      var ms = new Date(sessionExpiresAt).getTime() - Date.now();
      if (ms <= 0) {
        setState('expired');
        if (countdownTimer) clearInterval(countdownTimer);
      }
      emit();
    }, 1000);
  }

  function countdownText() {
    if (!sessionExpiresAt) return '—';
    var ms = new Date(sessionExpiresAt).getTime() - Date.now();
    if (ms <= 0) return 'expired';
    var sec = Math.floor(ms / 1000);
    var min = Math.floor(sec / 60);
    return min + ':' + String(sec % 60).padStart(2, '0');
  }

  function authHeaders() {
    return { 'Authorization': 'Bearer ' + token };
  }

  return {
    init: init, onChange: onChange, checkSession: checkSession,
    startPolling: startPolling, stopPolling: stopPolling,
    labelFor: labelFor, countdownText: countdownText,
    authHeaders: authHeaders, getState: function () { return state; },
    getLastSync: function () { return lastSync; },
    getToken: function () { return token; },
    getImportTarget: function () { return importTarget; },
    isTransactionSession: function () { return importTarget === 'Transaction'; }
  };
})();

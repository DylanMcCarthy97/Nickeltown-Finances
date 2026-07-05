window.NFSettings = (function () {
  'use strict';

  var KEY = 'nf-pwa-settings-v1';
  var defaults = {
    theme: 'auto',
    quality: 'high',
    rearCamera: true,
    haptics: true
  };

  function load() {
    try {
      return Object.assign({}, defaults, JSON.parse(localStorage.getItem(KEY) || '{}'));
    } catch (e) {
      return Object.assign({}, defaults);
    }
  }

  function save(partial) {
    var next = Object.assign(load(), partial);
    localStorage.setItem(KEY, JSON.stringify(next));
    return next;
  }

  function resolveTheme(settings) {
    if (settings.theme === 'auto') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return settings.theme;
  }

  function qualityFactor(quality) {
    switch (quality) {
      case 'original': return 1;
      case 'high': return 0.88;
      case 'medium': return 0.72;
      case 'low': return 0.55;
      default: return 0.88;
    }
  }

  function maxDimension(quality) {
    switch (quality) {
      case 'original': return 4096;
      case 'high': return 2400;
      case 'medium': return 1600;
      case 'low': return 1024;
      default: return 2400;
    }
  }

  return { load: load, save: save, resolveTheme: resolveTheme, qualityFactor: qualityFactor, maxDimension: maxDimension, defaults: defaults };
})();

mergeInto(LibraryManager.library, {
  US_Vibrate: function (ms) {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {}
  },
  US_IsTouchDevice: function () {
    try {
      return (('ontouchstart' in window) || navigator.maxTouchPoints > 0 ||
              (window.matchMedia && window.matchMedia('(pointer: coarse)').matches)) ? 1 : 0;
    } catch (e) { return 0; }
  },
  US_Share: function (textPtr) {
    var text = UTF8ToString(textPtr);
    var url = window.location.href.split('#')[0];
    function toast(msg) { if (window.usToast) window.usToast(msg); }
    function copy() {
      var full = text + ' ' + url;
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(full).then(function () { toast('Link copied!'); },
          function () { toast(url); });
      } else { toast(url); }
    }
    try {
      if (navigator.share) {
        navigator.share({ title: 'Urban Sunset', text: text, url: url }).catch(function (e) {
          if (!e || e.name !== 'AbortError') copy();
        });
      } else { copy(); }
    } catch (e) { copy(); }
  },
  US_GameReady: function () {
    if (window.usGameReady) window.usGameReady();
  }
});

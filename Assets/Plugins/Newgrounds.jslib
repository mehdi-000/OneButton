// NewGrounds.io v3 SDK bridge for Unity WebGL.
//
// Expected host page setup (in the WebGL template's index.html):
//   <script src="https://www.newgrounds.io/downloads/newgrounds_io.js"></script>
//
// After loading, `window.newgrounds` exposes `io.core` and helpers.
// All calls below are best-effort: they log + skip silently if the SDK is missing,
// so editor / non-NG hosts don't crash.

mergeInto(LibraryManager.library, {

  NG_Init: function (appIdPtr, encryptionKeyPtr) {
    try {
      var appId = UTF8ToString(appIdPtr);
      var key = UTF8ToString(encryptionKeyPtr);
      if (typeof newgrounds === "undefined") {
        console.warn("[NG] newgrounds.io SDK not loaded");
        return;
      }
      window.__ngCore = new newgrounds.io.core(appId, key);
      console.log("[NG] Init", appId);
    } catch (e) { console.error("[NG] Init failed", e); }
  },

  NG_LogView: function () {
    try {
      if (!window.__ngCore) return;
      var c = new newgrounds.io.components.App.logView();
      window.__ngCore.executeComponent(c);
    } catch (e) { console.error("[NG] LogView failed", e); }
  },

  NG_PostScore: function (boardId, value) {
    try {
      if (!window.__ngCore || !boardId) return;
      var c = new newgrounds.io.components.ScoreBoard.postScore({
        id: boardId,
        value: value,
      });
      window.__ngCore.executeComponent(c, function (result) {
        if (!result || !result.success) console.warn("[NG] PostScore failed", result);
      });
    } catch (e) { console.error("[NG] PostScore failed", e); }
  },

  NG_UnlockMedal: function (medalId) {
    try {
      if (!window.__ngCore || !medalId) return;
      var c = new newgrounds.io.components.Medal.unlock({ id: medalId });
      window.__ngCore.executeComponent(c, function (result) {
        if (!result || !result.success) console.warn("[NG] UnlockMedal failed", result);
      });
    } catch (e) { console.error("[NG] UnlockMedal failed", e); }
  },

});

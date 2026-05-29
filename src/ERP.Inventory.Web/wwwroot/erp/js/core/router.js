window.Router = {
  pages: {},
  current: null,

  register(name, render) {
    this.pages[name] = render;
  },

  go(screen) {
    if (!screen) return;

    if (typeof window.canOpenScreen === 'function' && !window.canOpenScreen(screen)) {
      window.Router.go(window.AppConfig.defaultRoute || 'dashboard');
      return;
    }

    this.current = screen;
    if (window.location.hash !== '#' + screen) window.location.hash = screen;

    $('.nav-link-js').removeClass('active');
    $(`.nav-link-js[data-screen="${screen}"]`).addClass('active');
    $('#drawer').removeClass('open right-drawer-detail');


      const page = this.pages[screen] || this.pages['operation'];
      this.updatePrintButton(screen);
    if (page) page(screen);
    },
    updatePrintButton(type) {
        const canShow = AppConfig.canPrintPDF.includes(type);
        $('#btnPrintVoucher').toggleClass('d-none', !canShow);
    }

};

// Single hashchange listener — prevents double-fire with app.js bootstrap
window.addEventListener('hashchange', () => {
  const screen = window.location.hash.replace('#', '');
  if (screen && screen !== Router.current) {
    Router.go(screen);
  }
});

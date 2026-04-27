window.Router = {
  pages: {},
  current: null,
  register(name, render){ this.pages[name] = render; },
  go(screen){
    this.current = screen;
    $('.nav-link-js').removeClass('active');
    $(`.nav-link-js[data-screen="${screen}"]`).addClass('active');
    $('#drawer').removeClass('open');
    const page = this.pages[screen] || this.pages['operation'];
    page(screen);
  }
};

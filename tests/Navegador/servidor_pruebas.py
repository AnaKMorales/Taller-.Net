# Sirve la raíz del repo para la prueba del cifrado de la PWA y guarda en $RESULTADOS lo que la página informa por POST.
import http.server, os, sys
class H(http.server.SimpleHTTPRequestHandler):
    def do_POST(self):
        n = int(self.headers.get('Content-Length', 0)); datos = self.rfile.read(n)
        open(os.path.join(os.environ['RESULTADOS'], self.path.strip('/') + '.txt'), 'wb').write(datos)
        self.send_response(204); self.end_headers()
    def log_message(self, *a): pass
http.server.ThreadingHTTPServer(('127.0.0.1', 5395), H).serve_forever()

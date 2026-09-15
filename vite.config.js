import { defineConfig } from 'vite';

const API_TARGET = process.env.VITE_API_PROXY || 'http://localhost:5080';

// أثناء التطوير: أي طلب يبدأ بـ /api يُحوَّل إلى الـ .NET API
export default defineConfig({
  // مسارات نسبية للملفات عشان النسخة تشتغل من جذر الموقع أو من فولدر فرعي
  base: './',
  server: {
    proxy: {
      '/api': {
        target: API_TARGET,
        changeOrigin: true,
        configure: proxy => {
          // لو الـ API مش شغال نرجّع رسالة واضحة بدل 500 فاضي
          proxy.on('error', (err, _req, res) => {
            if (!res || typeof res.writeHead !== 'function' || res.headersSent) return;
            res.writeHead(502, { 'Content-Type': 'application/json; charset=utf-8' });
            res.end(JSON.stringify({
              status: 502,
              title: `الـ API غير متاح على ${API_TARGET} — تأكد إنه شغّال (dotnet run) وإنه ما وقفش بسبب خطأ في الاتصال بقاعدة البيانات.`,
              detail: err.code || err.message
            }));
          });
        }
      }
    }
  }
});

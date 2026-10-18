import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "إحصائية مدني كلي | نموذج إدخال تفاعلي",
  description: "تجربة إدخال الإنتاج وأحكام القضاة وتوزيع المؤجلات يدويًا حسب سنة القيد.",
  icons: {
    icon: "/favicon.svg",
    shortcut: "/favicon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ar" dir="rtl">
      <body className="antialiased">{children}</body>
    </html>
  );
}

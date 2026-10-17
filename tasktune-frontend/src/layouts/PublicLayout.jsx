export default function PublicLayout({ children }) {
  return (
    <div className="min-h-screen bg-bg flex items-center justify-center">
      {children}
    </div>
  );
}

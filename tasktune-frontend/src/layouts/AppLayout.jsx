import Sidebar from '../components/Sidebar';
import ChatbotWidget from '../components/ChatbotWidget';
import TopHeader from '../components/TopHeader';

export default function AppLayout({ title, children }) {
  return (
    <div className="min-h-screen bg-bg">
      <Sidebar />
      <div className="ml-[260px] flex flex-col min-h-screen">
        <TopHeader title={title} />
        <main className="flex-1 p-6 overflow-y-auto">
          <div className="max-w-[1440px] mx-auto">
            {children}
          </div>
        </main>
      </div>
      <ChatbotWidget />
    </div>
  );
}

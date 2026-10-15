import { AppRoutes } from "./app/AppRoutes";
import { AppLayout } from "./components/layout/AppLayout";

function App() {
  return (
    <>
      <AppLayout>
        <AppRoutes />
      </AppLayout>
    </>
  );
}

export default App;

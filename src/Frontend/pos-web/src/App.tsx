import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AuthProvider } from './context/AuthContext';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { CashPage } from './pages/CashPage';
import { CheckoutPage } from './pages/CheckoutPage';
import { CustomersPage } from './pages/CustomersPage';
import { FloorPlanPage } from './pages/FloorPlanPage';
import { KdsPage } from './pages/KdsPage';
import { OrderPage } from './pages/OrderPage';
import { ProductsPage } from './pages/ProductsPage';
import { StockPage } from './pages/StockPage';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <DashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/products"
            element={
              <ProtectedRoute>
                <ProductsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/stock"
            element={
              <ProtectedRoute>
                <StockPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/checkout"
            element={
              <ProtectedRoute>
                <CheckoutPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/restaurant"
            element={
              <ProtectedRoute>
                <FloorPlanPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/restaurant/orders/:orderId"
            element={
              <ProtectedRoute>
                <OrderPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/kds"
            element={
              <ProtectedRoute>
                <KdsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/cash"
            element={
              <ProtectedRoute>
                <CashPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/customers"
            element={
              <ProtectedRoute>
                <CustomersPage />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

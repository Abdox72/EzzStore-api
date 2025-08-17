# Ezz Project - Payment Gateway & Google Login Setup

This guide will help you set up the payment gateway (Stripe) and Google login features for your Ezz project.

## 🚀 New Features Added

### 1. Google Login Integration
- Firebase Authentication for Google OAuth
- Backend Google token verification
- Seamless user registration/login flow

### 2. Payment Gateway (Stripe)
- Secure payment processing
- Payment intent creation and confirmation
- Payment history tracking
- Refund functionality

## 📋 Prerequisites

### For Google Login:
1. **Google Cloud Console Setup**
   - Go to [Google Cloud Console](https://console.cloud.google.com/)
   - Create a new project or select existing one
   - Enable Google+ API
   - Create OAuth 2.0 credentials
   - Get Client ID and Client Secret

2. **Firebase Setup** (Optional - for enhanced frontend integration)
   - Go to [Firebase Console](https://console.firebase.google.com/)
   - Create a new project
   - Enable Authentication with Google provider
   - Get Firebase configuration

### For Stripe Payments:
1. **Stripe Account**
   - Sign up at [Stripe](https://stripe.com/)
   - Get your API keys (Publishable and Secret keys)
   - Set up webhook endpoints (optional)

## ⚙️ Configuration

### 1. Backend Configuration (.NET API)

Update `Ezz-api/Ezz-api/appsettings.json`:

```json
{
  "Google": {
    "ClientId": "your-google-client-id",
    "ClientSecret": "your-google-client-secret"
  },
  "Stripe": {
    "SecretKey": "sk_test_your_stripe_secret_key",
    "PublishableKey": "pk_test_your_stripe_publishable_key"
  }
}
```

### 2. Frontend Configuration (Angular)

Update `ezz/src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7249/api',
  firebase: {
    apiKey: "your-firebase-api-key",
    authDomain: "your-project.firebaseapp.com",
    projectId: "your-project-id",
    storageBucket: "your-project.appspot.com",
    messagingSenderId: "your-sender-id",
    appId: "your-app-id"
  },
  stripe: {
    publishableKey: 'pk_test_your_stripe_publishable_key'
  }
};
```

Update `ezz/src/environments/environment.prod.ts` with production values.

## 🔧 Installation Steps

### 1. Install Dependencies

**Frontend (Angular):**
```bash
cd ezz
npm install
```

**Backend (.NET):**
```bash
cd Ezz-api
dotnet restore
```

### 2. Database Migration

```bash
cd Ezz-api/Ezz-api
dotnet ef database update
```

### 3. Add Stripe Script to Angular

Add the Stripe script to `ezz/src/index.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <!-- ... existing head content ... -->
  <script src="https://js.stripe.com/v3/"></script>
</head>
<body>
  <!-- ... existing body content ... -->
</body>
</html>
```

## 🚀 Usage

### Google Login

1. **Frontend Implementation:**
   - The login component now includes a "Login with Google" button
   - Users can click to authenticate with their Google account
   - Automatic user creation if email doesn't exist

2. **Backend Flow:**
   - Receives Google ID token
   - Verifies token with Google's servers
   - Creates or retrieves user account
   - Returns JWT token for session management

### Payment Processing

1. **Create Payment Intent:**
   ```typescript
   const paymentRequest: PaymentRequest = {
     amount: 1000, // $10.00 in cents
     currency: 'usd',
     description: 'Product purchase',
     customerEmail: 'user@example.com'
   };
   
   this.paymentService.createPaymentIntent(paymentRequest).subscribe(
     (intent) => {
       // Handle payment intent creation
     }
   );
   ```

2. **Confirm Payment:**
   ```typescript
   this.paymentService.confirmPayment(paymentIntentId, paymentMethodId).subscribe(
     (result) => {
       if (result.success) {
         // Payment successful
       }
     }
   );
   ```

3. **Payment Component Usage:**
   ```html
   <app-payment 
     [amount]="1000" 
     [description]="'Product purchase'"
     (onPaymentComplete)="handlePaymentComplete()">
   </app-payment>
   ```

## 🔒 Security Considerations

### Google Authentication:
- Always verify Google ID tokens on the backend
- Use HTTPS in production
- Implement proper session management

### Stripe Payments:
- Never expose secret keys in frontend code
- Use webhooks for payment status updates
- Implement proper error handling
- Store payment metadata for audit trails

## 🧪 Testing

### Google Login Testing:
1. Use test Google accounts
2. Test both new user registration and existing user login
3. Verify email confirmation flow

### Stripe Testing:
1. Use Stripe test cards:
   - Success: `4242 4242 4242 4242`
   - Decline: `4000 0000 0000 0002`
   - Requires authentication: `4000 0025 0000 3155`

2. Test different scenarios:
   - Successful payments
   - Failed payments
   - Refunds
   - Payment history

## 🐛 Troubleshooting

### Common Issues:

1. **Google Login Not Working:**
   - Verify Client ID and Secret are correct
   - Check authorized redirect URIs
   - Ensure Google+ API is enabled

2. **Stripe Payments Failing:**
   - Verify API keys are correct
   - Check CORS settings
   - Ensure proper error handling

3. **Database Issues:**
   - Run migrations: `dotnet ef database update`
   - Check connection string
   - Verify SQLite database file exists

## 📚 Additional Resources

- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [Stripe Documentation](https://stripe.com/docs)
- [Firebase Authentication](https://firebase.google.com/docs/auth)
- [Angular Material](https://material.angular.io/)

## 🤝 Support

If you encounter any issues during setup, please:
1. Check the troubleshooting section
2. Verify all configuration values
3. Ensure all dependencies are installed
4. Check browser console and server logs for errors

---

**Note:** Remember to replace all placeholder values (`your-google-client-id`, `your-stripe-secret-key`, etc.) with your actual credentials before deploying to production. 
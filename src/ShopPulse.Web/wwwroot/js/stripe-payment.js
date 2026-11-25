// Interop Stripe.js (phase 6) : chargement paresseux de https://js.stripe.com/v3/,
// montage du Payment Element, confirmation. Le résultat du navigateur n'est JAMAIS
// une preuve de paiement : seul le webhook signé côté API fait passer la commande à Paid.
let stripe = null;
let elements = null;

async function ensureStripeJs() {
    if (window.Stripe) return window.Stripe;
    await new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = 'https://js.stripe.com/v3/';
        s.onload = resolve;
        s.onerror = () => reject(new Error('Chargement de Stripe.js impossible.'));
        document.head.appendChild(s);
    });
    return window.Stripe;
}

export async function mountPaymentElement(publishableKey, clientSecret, containerId) {
    const StripeFactory = await ensureStripeJs();
    stripe = StripeFactory(publishableKey);
    elements = stripe.elements({ clientSecret });
    const paymentElement = elements.create('payment');
    paymentElement.mount('#' + containerId);
}

// Retourne null si la confirmation est partie sans erreur immédiate, sinon le message.
// redirect: 'if_required' : pas de redirection pour une carte sans 3DS.
export async function confirmPayment(returnUrl) {
    if (!stripe || !elements) return 'Le formulaire de paiement n’est pas initialisé.';
    const { error } = await stripe.confirmPayment({
        elements,
        confirmParams: { return_url: returnUrl },
        redirect: 'if_required',
    });
    return error ? (error.message ?? 'Paiement refusé.') : null;
}

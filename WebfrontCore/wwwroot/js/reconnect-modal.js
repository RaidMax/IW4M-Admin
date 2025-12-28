// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

// All possible state classes that can be applied to the modal
const stateClasses = [
    "components-reconnect-show",
    "components-reconnect-retrying",
    "components-reconnect-failed",
    "components-reconnect-paused",
    "components-reconnect-resume-failed"
];

function setModalState(state) {
    // Remove all state classes first
    reconnectModal.classList.remove(...stateClasses);
    
    // Add the new state class if it's a valid state
    const stateClass = `components-reconnect-${state}`;
    if (stateClasses.includes(stateClass)) {
        reconnectModal.classList.add(stateClass);
    }
}

function handleReconnectStateChanged(event) {
    const state = event.detail.state;
    
    // Apply the state class to toggle visibility of appropriate elements
    setModalState(state);
    
    if (state === "show") {
        reconnectModal.showModal();
    } else if (state === "hide") {
        reconnectModal.close();
    } else if (state === "failed") {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (state === "rejected") {
        location.reload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // Reconnect will asynchronously return:
        // - true to mean success
        // - false to mean we reached the server, but it rejected the connection (e.g., unknown circuit ID)
        // - exception to mean we didn't reach the server (this can be sync or async)
        const successful = await Blazor.reconnect();
        if (!successful) {
            // We have been able to reach the server, but the circuit is no longer available.
            // We'll reload the page so the user can continue using the app as quickly as possible.
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                location.reload();
            } else {
                reconnectModal.close();
            }
        }
    } catch (err) {
        // We got an exception, server is currently unavailable
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        }
    } catch {
        location.reload();
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}

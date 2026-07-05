window.infiniteScroll = {
    observers: {},

    // Initializes an IntersectionObserver against the given element.
    // methodName defaults to "LoadMore" for backward compatibility.
    // Multiple observers may exist concurrently (keyed by elementId).
    initialize: function (dotNetHelper, elementId, methodName) {
        const element = document.getElementById(elementId);
        if (!element) return;

        // Replace any existing observer for this element (e.g., re-init after reset).
        this.disconnect(elementId);

        const options = {
            root: null,
            rootMargin: '200px', // Preload before reaching bottom.
            threshold: 0
        };

        const callbackName = methodName || 'LoadMore';
        const observer = new IntersectionObserver(async (entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    await dotNetHelper.invokeMethodAsync(callbackName);
                }
            }
        }, options);

        observer.observe(element);
        this.observers[elementId] = observer;
    },

    // Single-arg form disconnects a specific observer; no-arg form disconnects
    // all (legacy callers that initialized with the singleton pattern).
    disconnect: function (elementId) {
        if (elementId) {
            const obs = this.observers[elementId];
            if (obs) {
                obs.disconnect();
                delete this.observers[elementId];
            }
            return;
        }
        Object.values(this.observers).forEach(o => o.disconnect());
        this.observers = {};
    }
};

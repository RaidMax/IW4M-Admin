window.infiniteScroll = {
    observer: null,

    initialize: function (dotNetHelper, elementId) {
        const element = document.getElementById(elementId);
        if (!element) return;

        // Cleanup existing observer if any
        if (this.observer) {
            this.disconnect();
        }

        const options = {
            root: null,
            rootMargin: '100px', // Preload before reaching bottom
            threshold: 0.1
        };

        this.observer = new IntersectionObserver(async (entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    await dotNetHelper.invokeMethodAsync('LoadMore');
                }
            }
        }, options);

        this.observer.observe(element);
    },

    disconnect: function () {
        if (this.observer) {
            this.observer.disconnect();
            this.observer = null;
        }
    }
};

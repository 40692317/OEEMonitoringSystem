// Global variables for connection state
var connection = null;
var isConnected = false;
var retryCount = 0;
var maxRetries = 5;
var subscribedMachines = new Set();
var isPageVisible = true;
var lastUpdateTimes = new Map(); // Track last update times for each machine

// Initialize SignalR connection with better error handling
function initializeSignalR() {
    try {
        // Check if SignalR is available
        if (typeof signalR === 'undefined') {
            console.error("❌ SignalR library not found. Please check the script path.");
            return;
        }

        // Create connection
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/oeehub")
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: function (retryContext) {
                    console.log("Retry attempt: " + retryContext.previousRetryCount);
                    return retryContext.previousRetryCount < 5 ? 1000 : 30000;
                }
            })
            .build();

        // Connection started
        connection.start()
            .then(function () {
                isConnected = true;
                retryCount = 0;
                console.log("✅ SignalR connected successfully!");
                console.log("Connection ID:", connection.connection.id);

                // Subscribe to all machines on dashboard
                subscribeToAllMachines();

                // Send connection success message
                if (typeof window.onSignalRConnected === 'function') {
                    window.onSignalRConnected();
                }
            })
            .catch(function (error) {
                console.error("❌ SignalR connection failed:", error.message);
                isConnected = false;

                // Retry connection
                if (retryCount < maxRetries) {
                    retryCount++;
                    console.log(`🔄 Retrying connection (${retryCount}/${maxRetries})...`);
                    setTimeout(initializeSignalR, 3000);
                } else {
                    console.error("❌ Max retry attempts reached. Connection failed permanently.");
                }
            });

        // Handle reconnection
        connection.onreconnecting(function (error) {
            console.log("🔄 SignalR reconnecting...", error);
        });

        connection.onreconnected(function (connectionId) {
            console.log("✅ SignalR reconnected with ID:", connectionId);
            retryCount = 0;
            // Resubscribe to all machines after reconnection
            subscribeToAllMachines();
        });

        // Handle connection closed
        connection.onclose(function (error) {
            isConnected = false;
            console.log("❌ SignalR connection closed:", error);

            // Attempt to reconnect
            if (retryCount < maxRetries) {
                retryCount++;
                console.log(`🔄 Attempting to reconnect (${retryCount}/${maxRetries})...`);
                setTimeout(initializeSignalR, 3000);
            }
        });

        // Handle incoming messages
        connection.on("OEEUpdated", function (data) {
            console.log("📡 Received OEE update:", data);
            updateOEECard(data.machineId, data.oee, data.availability, data.performance, data.quality, data.EventType, data.machineState, data.predictedOEE);
        });

        // Handle subscription success
        connection.on("SubscriptionSuccess", function (data) {
            console.log("✅ Subscription successful:", data);
        });

        // Handle subscription error
        connection.on("SubscriptionError", function (data) {
            console.error("❌ Subscription error:", data);
        });

        // Handle unsubscription success
        connection.on("UnsubscriptionSuccess", function (data) {
            console.log("✅ Unsubscription successful:", data);
        });

        // Handle unsubscription error
        connection.on("UnsubscriptionError", function (data) {
            console.error("❌ Unsubscription error:", data);
        });

    } catch (error) {
        console.error("❌ Error initializing SignalR:", error);
    }
}

// Subscribe to all machines on the dashboard
function subscribeToAllMachines() {
    try {
        // Clear existing subscriptions
        subscribedMachines.clear();

        var machineCards = document.querySelectorAll('[data-machine-id]');
        console.log("Found " + machineCards.length + " machine cards to subscribe to");

        machineCards.forEach(function (card) {
            var machineId = card.getAttribute('data-machine-id');
            if (machineId) {
                console.log("Subscribing to machine:", machineId);
                if (connection && isConnected) {
                    connection.invoke("SubscribeToMachine", parseInt(machineId))
                        .then(function () {
                            console.log("✅ Successfully subscribed to machine:", machineId);
                            subscribedMachines.add(parseInt(machineId));

                            // Add visual feedback
                            var cardElement = document.querySelector(`[data-machine-id="${machineId}"]`);
                            if (cardElement) {
                                cardElement.classList.add('machine-card', 'update');
                                setTimeout(() => {
                                    cardElement.classList.remove('update');
                                }, 500);
                            }
                        })
                        .catch(function (error) {
                            console.error("❌ Error subscribing to machine:", machineId, error);
                        });
                } else {
                    console.log("⚠️ Connection not ready, skipping subscription for machine:", machineId);
                }
            }
        });
    } catch (error) {
        console.error("❌ Error in subscribeToAllMachines:", error);
    }
}

// Update OEE card UI with smooth animations (Phase 4: machineState, predictedOEE)
function updateOEECard(machineId, oee, availability, performance, quality, eventType = null, machineState = null, predictedOEE = null) {
    try {
        var card = document.querySelector(`[data-machine-id="${machineId}"]`);
        if (card) {
            // Add visual feedback for update
            card.classList.add('card-update');
            setTimeout(() => {
                card.classList.remove('card-update');
            }, 300);

            // Update OEE percentage with animation
            var oeeElement = card.querySelector('.oee-percentage');
            if (oeeElement) {
                // Add pulse animation for significant changes
                var oldValue = parseFloat(oeeElement.textContent) || 0;
                var diff = Math.abs(oldValue - oee);

                if (diff > 5) { // Significant change (>5%)
                    oeeElement.classList.add('pulse');
                    setTimeout(() => {
                        oeeElement.classList.remove('pulse');
                    }, 500);
                }

                // Smooth transition for the value
                oeeElement.classList.add('fade-in');
                oeeElement.textContent = oee.toFixed(2) + '%';

                // Remove class after animation
                setTimeout(() => {
                    oeeElement.classList.remove('fade-in');
                }, 300);
            }

            // Update progress bar with smooth animation
            var progressBar = card.querySelector('.progress-bar');
            if (progressBar) {
                // Store target width for animation
                progressBar.style.setProperty('--target-width', oee + '%');

                // Add animated class
                progressBar.classList.add('progress-bar-animated');

                // Update width with smooth transition
                progressBar.style.width = oee + '%';
                progressBar.textContent = oee.toFixed(2) + '%';

                // Remove animated class after animation
                setTimeout(() => {
                    progressBar.classList.remove('progress-bar-animated');
                }, 500);
            }

            // Update individual metrics with smooth transitions
            var availabilityElement = card.querySelector('[data-metric="availability"]');
            if (availabilityElement) {
                availabilityElement.classList.add('number-change');
                availabilityElement.textContent = availability.toFixed(2);
                setTimeout(() => {
                    availabilityElement.classList.remove('number-change');
                }, 200);
            }

            var performanceElement = card.querySelector('[data-metric="performance"]');
            if (performanceElement) {
                performanceElement.classList.add('number-change');
                performanceElement.textContent = performance.toFixed(2);
                setTimeout(() => {
                    performanceElement.classList.remove('number-change');
                }, 200);
            }

            var qualityElement = card.querySelector('[data-metric="quality"]');
            if (qualityElement) {
                qualityElement.classList.add('number-change');
                qualityElement.textContent = quality.toFixed(2);
                setTimeout(() => {
                    qualityElement.classList.remove('number-change');
                }, 200);
            }

            // Update timestamp if available
            var timestampElement = card.querySelector('.timestamp');
            if (timestampElement) {
                timestampElement.textContent = new Date().toLocaleTimeString();
            }

            // Update machine state badge (Phase 4)
            var stateBadge = card.querySelector('[data-role="machine-state"]');
            if (stateBadge && machineState) {
                stateBadge.textContent = machineState;
                stateBadge.className = 'badge me-1 ' + (machineState === 'Critical' ? 'bg-danger' : machineState === 'Warning' ? 'bg-warning text-dark' : 'bg-success');
            }
            // Update predicted OEE line (Phase 4)
            var predEl = card.querySelector('[data-role="predicted-oee"]');
            if (predEl) {
                if (predictedOEE != null && predictedOEE !== undefined) {
                    predEl.style.display = '';
                    predEl.innerHTML = '<i class="bi bi-graph-up-arrow"></i> Predicted OEE (ML): ' + parseFloat(predictedOEE).toFixed(1) + '%';
                } else {
                    predEl.style.display = 'none';
                }
            }

            // Add highlight effect for new events
            if (eventType) {
                card.classList.add('machine-card');
                card.classList.add('update');
                setTimeout(() => {
                    card.classList.remove('update');
                }, 500);
            }

            console.log("✅ Updated card for machine:", machineId, "OEE:", oee.toFixed(2) + '%');
        } else {
            console.log("⚠️ Card not found for machine:", machineId);
        }
    } catch (error) {
        console.error("❌ Error updating OEE card:", error);
    }
}

// Manual refresh function
function refreshDashboard() {
    try {
        console.log("🔄 Refreshing dashboard...");
        location.reload();
    } catch (error) {
        console.error("❌ Error refreshing dashboard:", error);
    }
}

// Page visibility change handler
function handlePageVisibilityChange() {
    if (document.hidden) {
        isPageVisible = false;
        console.log("Page is hidden");
    } else {
        isPageVisible = true;
        console.log("Page is visible");
        // When page becomes visible, resubscribe to ensure connection is active
        if (isConnected) {
            setTimeout(subscribeToAllMachines, 1000);
        }
    }
}

// Add page visibility change listener
document.addEventListener('visibilitychange', handlePageVisibilityChange);

// Add window focus listener
window.addEventListener('focus', function () {
    console.log("Window focused - resubscribing to machines");
    if (isConnected) {
        setTimeout(subscribeToAllMachines, 500);
    }
});

// Initialize when page loads
document.addEventListener('DOMContentLoaded', function () {
    console.log("DOMContentLoaded event fired");
    initializeSignalR();

 
});


// Add CSS animations to existing HTML elements
function addAnimationClasses() {
    // Add animation classes to existing elements
    const oeeElements = document.querySelectorAll('.oee-percentage');
    oeeElements.forEach(el => {
        el.classList.add('fade-in');
    });

    const progressBars = document.querySelectorAll('.progress-bar');
    progressBars.forEach(bar => {
        bar.classList.add('progress-bar-animated');
    });
}



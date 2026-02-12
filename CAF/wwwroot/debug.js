const API_BASE = '';

document.addEventListener('DOMContentLoaded', () => {
    const debugInput = document.getElementById('debugInput');
    const processButton = document.getElementById('processButton');
    const loadingSpinner = document.getElementById('loadingSpinner');
    const errorMessage = document.getElementById('errorMessage');
    const successMessage = document.getElementById('successMessage');
    const outputSection = document.getElementById('outputSection');
    const stateInfoGrid = document.getElementById('stateInfoGrid');
    const providerBadge = document.getElementById('providerBadge');
    const formattedRequest = document.getElementById('formattedRequest');
    const rawRequest = document.getElementById('rawRequest');
    const copyFormattedButton = document.getElementById('copyFormattedButton');
    const copyRawButton = document.getElementById('copyRawButton');
    const loadTokenCountsButton = document.getElementById('loadTokenCountsButton');
    const tokenCountsContainer = document.getElementById('tokenCountsContainer');
    const tokenCountsSummary = document.getElementById('tokenCountsSummary');
    const tokenCountsGrid = document.getElementById('tokenCountsGrid');

    let currentResponse = null;
    let tokenCountsData = null;

    // Simple tab switching
    const rawTab = document.getElementById('raw-tab');
    const formattedTab = document.getElementById('formatted-tab');
    const rawPane = document.getElementById('raw');
    const formattedPane = document.getElementById('formatted');
    
    if (rawTab && formattedTab && rawPane && formattedPane) {
        rawTab.addEventListener('click', () => {
            console.log('Raw tab clicked');
            
            // Update button styles
            rawTab.classList.remove('btn-outline-primary');
            rawTab.classList.add('btn-primary');
            formattedTab.classList.remove('btn-primary');
            formattedTab.classList.add('btn-outline-primary');
            
            // Show/hide panes
            rawPane.style.display = 'block';
            formattedPane.style.display = 'none';
            
            // Update content if needed
            if (currentResponse) {
                updateRawJSON();
            }
        });
        
        formattedTab.addEventListener('click', () => {
            console.log('Formatted tab clicked');
            
            // Update button styles
            formattedTab.classList.remove('btn-outline-primary');
            formattedTab.classList.add('btn-primary');
            rawTab.classList.remove('btn-primary');
            rawTab.classList.add('btn-outline-primary');
            
            // Show/hide panes
            formattedPane.style.display = 'block';
            rawPane.style.display = 'none';
        });
    }

    processButton.addEventListener('click', async () => {
        const input = debugInput.value.trim();
        
        if (!input) {
            showError('Please enter a message to process');
            return;
        }

        await processInput(input);
    });

    // Allow Enter to submit (with Shift+Enter for new line)
    debugInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            processButton.click();
        }
    });

    copyFormattedButton.addEventListener('click', () => {
        if (currentResponse) {
            const formatted = formatRequestForDisplay(currentResponse);
            copyToClipboard(formatted);
            showSuccess('Formatted request copied to clipboard!');
        }
    });

    copyRawButton.addEventListener('click', () => {
        if (currentResponse) {
            const request = currentResponse.geminiRequest || currentResponse.claudeRequest;
            const json = JSON.stringify(request, null, 2);
            copyToClipboard(json);
            showSuccess('Raw JSON copied to clipboard!');
        }
    });

    loadTokenCountsButton.addEventListener('click', async () => {
        await loadTokenCounts();
    });

    async function processInput(input) {
        hideMessages();
        showLoading(true);
        outputSection.style.display = 'none';

        try {
            const response = await fetch(`${API_BASE}/api/conversation/debug`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ input })
            });

            console.log('Response status:', response.status, response.statusText);

            if (!response.ok) {
                let errorMsg = 'Failed to process input';
                try {
                    const error = await response.json();
                    console.error('Error response:', error);
                    errorMsg = error.error || error.message || errorMsg;
                } catch (e) {
                    const text = await response.text();
                    console.error('Error text:', text);
                    errorMsg = text || errorMsg;
                }
                throw new Error(errorMsg);
            }

            const data = await response.json();
            console.log('Success response:', data);
            currentResponse = data;
            
            displayResults(data);
            showLoading(false);
            outputSection.style.display = 'block';
            
            // Scroll to results
            setTimeout(() => {
                outputSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }, 100);
        } catch (error) {
            console.error('Error processing input:', error);
            showError(`Error: ${error.message}`);
            showLoading(false);
        }
    }

    function displayResults(data) {
        // Store data globally
        currentResponse = data;
        
        // Validate response structure
        if (!data) {
            console.error('No data in response');
            showError('Invalid response: no data received');
            return;
        }
        
        // Log the actual response for debugging
        console.log('Response received:', data);
        
        // Ensure state exists (handle null property exclusion)
        if (!data.state) {
            console.error('No state data in response', data);
            showError('Invalid response: missing state data. Check console for details.');
            return;
        }
        
        // Display state info with expandable sections
        displayStateInfo(data.state, data);

        // Display loaded context data token counts if available
        if (data.loadedContextData) {
            displayLoadedContextData(data.loadedContextData);
            tokenCountsContainer.style.display = 'block';
        }

        // Display provider badge with fallback
        const providerName = data.providerName || 'Unknown';
        const providerClass = providerName.toLowerCase() === 'claude' ? 'provider-claude' : 'provider-gemini';
        providerBadge.innerHTML = `<span class="provider-badge ${providerClass}">${providerName}</span>`;

        // Display formatted request
        const formatted = formatRequestForDisplay(data);
        formattedRequest.textContent = formatted;

        // Populate raw JSON immediately
        updateRawJSON();
    }

    function updateRawJSON() {
        if (!currentResponse) {
            return;
        }
        
        try {
            const request = currentResponse.geminiRequest || currentResponse.claudeRequest;
            
            if (!request) {
                rawRequest.textContent = 'No request data available';
                return;
            }
            
            // Simple JSON stringify with proper formatting
            const json = JSON.stringify(request, null, 2);
            rawRequest.textContent = json;
        } catch (error) {
            console.error('Error updating raw JSON:', error);
            rawRequest.textContent = `Error displaying JSON: ${error.message}`;
        }
    }

    function displayStateInfo(state, fullData) {
        const items = [
            { label: 'User Name', value: state.userName || 'N/A' },
            { label: 'Persona Name', value: state.personaName || 'N/A' },
            { label: 'OOC Request', value: state.isOOCRequest ? 'Yes' : 'No' },
            { label: 'Persona System Message', value: state.personaName_SystemMessage || 'None' },
            { 
                label: 'Persona Contexts', 
                value: state.personaContextCount || 0,
                expandable: (state.personaContextCount || 0) > 0,
                data: fullData.geminiRequest?.systemInstruction || fullData.claudeRequest?.system,
                type: 'persona-contexts'
            },
            { 
                label: 'Triggered Contexts', 
                value: state.triggeredContextCount || 0
            },
            { 
                label: 'Always-On Memories', 
                value: state.alwaysOnMemoryCount || 0
            },
            { 
                label: 'Flags', 
                value: state.flagCount || 0
            },
            { 
                label: 'Recent Turns', 
                value: state.recentTurnCount || 0
            },
            { 
                label: 'Perceptions', 
                value: state.perceptionCount || 0
            },
            { 
                label: 'Dynamic Quotes', 
                value: state.dynamicQuoteCount || 0,
                expandable: (state.dynamicQuoteCount || 0) > 0,
                type: 'dynamic-quotes'
            },
            { 
                label: 'Canon Quotes', 
                value: state.canonQuoteCount || 0,
                expandable: (state.canonQuoteCount || 0) > 0,
                type: 'canon-quotes'
            },
            { label: 'Dialogue Log Length', value: (state.dialogueLogLength || 0) + ' chars' },
            { label: 'Recent Context Length', value: (state.recentContextLength || 0) + ' chars' }
        ];

        stateInfoGrid.innerHTML = items.map((item, index) => {
            const hasExpand = item.expandable && item.value > 0;
            return `
            <div class="state-item ${hasExpand ? 'state-item-expandable' : ''}" ${hasExpand ? `data-type="${item.type}" data-index="${index}"` : ''}>
                <div class="state-item-header">
                    <div>
                        <div class="state-item-label">${item.label}</div>
                        <div class="state-item-value">${item.value}</div>
                    </div>
                    ${hasExpand ? '<button class="btn btn-sm btn-link expand-btn"><i class="fas fa-chevron-down"></i></button>' : ''}
                </div>
                ${hasExpand ? `<div class="state-item-content" style="display: none;"></div>` : ''}
            </div>
            `;
        }).join('');

        // Store full data for expansion
        window.fullDebugData = fullData;
        window.stateItems = items;

        // Add click handlers for expandable items
        document.querySelectorAll('.state-item-expandable').forEach(item => {
            const btn = item.querySelector('.expand-btn');
            const content = item.querySelector('.state-item-content');
            const type = item.dataset.type;
            const index = parseInt(item.dataset.index);

            btn.addEventListener('click', () => {
                const isExpanded = content.style.display !== 'none';
                
                if (isExpanded) {
                    content.style.display = 'none';
                    btn.innerHTML = '<i class="fas fa-chevron-down"></i>';
                } else {
                    // Populate content based on type
                    populateExpandedContent(content, type, fullData);
                    content.style.display = 'block';
                    btn.innerHTML = '<i class="fas fa-chevron-up"></i>';
                }
            });
        });
    }

    function populateExpandedContent(container, type, data) {
        let html = '';
        
        try {
            if (type === 'persona-contexts') {
                const systemData = data.geminiRequest?.systemInstruction || data.claudeRequest?.system;
                if (systemData) {
                    html = '<div class="expanded-content-item">';
                    
                    if (data.geminiRequest) {
                        // Gemini format
                        if (systemData.parts && systemData.parts.length > 0) {
                            systemData.parts.forEach((part, index) => {
                                if (part.text) {
                                    html += `<div class="context-section"><strong>Part ${index + 1}:</strong><pre class="expanded-pre">${escapeHtml(part.text.substring(0, 1000))}${part.text.length > 1000 ? '\n\n... (truncated, see Raw JSON for full content)' : ''}</pre></div>`;
                                }
                            });
                        }
                    } else if (data.claudeRequest) {
                        // Claude format
                        if (typeof systemData === 'string') {
                            html += `<pre class="expanded-pre">${escapeHtml(systemData.substring(0, 1000))}${systemData.length > 1000 ? '\n\n... (truncated, see Raw JSON for full content)' : ''}</pre>`;
                        } else if (Array.isArray(systemData)) {
                            systemData.forEach((block, index) => {
                                if (block.type === 'text' && block.text) {
                                    html += `<div class="context-section"><strong>Block ${index + 1}:</strong><pre class="expanded-pre">${escapeHtml(block.text.substring(0, 1000))}${block.text.length > 1000 ? '\n\n... (truncated)' : ''}</pre></div>`;
                                }
                            });
                        }
                    }
                    
                    html += '</div>';
                } else {
                    html = '<p class="text-muted">No system instruction found</p>';
                }
            } else if (type === 'dynamic-quotes' || type === 'canon-quotes') {
                // Look for quotes in the entire request
                html = '<div class="expanded-content-item">';
                
                const messages = data.geminiRequest?.contents || data.claudeRequest?.messages || [];
                const systemInstruction = data.geminiRequest?.systemInstruction || data.claudeRequest?.system;
                
                let allText = '';
                
                // Collect text from system instruction
                if (systemInstruction) {
                    if (data.geminiRequest && systemInstruction.parts) {
                        systemInstruction.parts.forEach(part => {
                            if (part.text) allText += part.text + '\n\n';
                        });
                    } else if (typeof systemInstruction === 'string') {
                        allText += systemInstruction + '\n\n';
                    } else if (Array.isArray(systemInstruction)) {
                        systemInstruction.forEach(block => {
                            if (block.text) allText += block.text + '\n\n';
                        });
                    }
                }
                
                // Collect text from messages
                messages.forEach(msg => {
                    if (data.geminiRequest && msg.parts) {
                        msg.parts.forEach(part => {
                            if (part.text) allText += part.text + '\n\n';
                        });
                    } else if (data.claudeRequest) {
                        if (typeof msg.content === 'string') {
                            allText += msg.content + '\n\n';
                        } else if (Array.isArray(msg.content)) {
                            msg.content.forEach(block => {
                                if (block.text) allText += block.text + '\n\n';
                            });
                        }
                    }
                });
                
                // Look for quote sections
                const quoteType = type === 'dynamic-quotes' ? 'Dynamic' : 'Canon';
                const quotePattern = new RegExp(`###\\s*${quoteType}\\s+Quote\\s+\\d+:([\\s\\S]*?)(?=###|$)`, 'gi');
                const matches = [...allText.matchAll(quotePattern)];
                
                if (matches.length > 0) {
                    matches.forEach((match, index) => {
                        const quoteText = match[1].trim();
                        html += `<div class="quote-item"><strong>${quoteType} Quote ${index + 1}:</strong><pre class="expanded-pre">${escapeHtml(quoteText.substring(0, 500))}${quoteText.length > 500 ? '\n\n... (truncated)' : ''}</pre></div>`;
                    });
                } else {
                    html += `<p class="text-muted">No ${quoteType.toLowerCase()} quotes found with standard markers. Check Raw JSON or Formatted View for embedded quotes.</p>`;
                }
                
                html += '</div>';
            }
        } catch (error) {
            console.error('Error populating expanded content:', error);
            html = `<p class="text-danger">Error displaying content: ${error.message}</p>`;
        }
        
        container.innerHTML = html;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function formatRequestForDisplay(data) {
        if (data.geminiRequest) {
            return formatGeminiRequest(data.geminiRequest);
        } else if (data.claudeRequest) {
            return formatClaudeRequest(data.claudeRequest);
        }
        return 'No request data available';
    }

    function formatGeminiRequest(request) {
        let output = '=== GEMINI REQUEST ===\n\n';
        
        // System Instruction
        if (request.systemInstruction) {
            output += '--- SYSTEM INSTRUCTION ---\n';
            if (request.systemInstruction.parts) {
                request.systemInstruction.parts.forEach((part, i) => {
                    if (part.text) {
                        output += `${part.text}\n`;
                    }
                });
            }
            output += '\n';
        }

        // Contents (messages)
        if (request.contents && request.contents.length > 0) {
            output += '--- MESSAGES ---\n\n';
            request.contents.forEach((content, i) => {
                const role = content.role ? content.role.toUpperCase() : 'UNKNOWN';
                output += `[${role}]\n`;
                
                if (content.parts) {
                    content.parts.forEach(part => {
                        if (part.text) {
                            output += `${part.text}\n`;
                        }
                    });
                }
                output += '\n';
            });
        }

        // Generation Config
        if (request.generationConfig) {
            output += '--- GENERATION CONFIG ---\n';
            output += JSON.stringify(request.generationConfig, null, 2) + '\n';
        }

        return output;
    }

    function formatClaudeRequest(request) {
        let output = '=== CLAUDE REQUEST ===\n\n';
        
        // Model info
        output += `Model: ${request.model || 'N/A'}\n`;
        output += `Max Tokens: ${request.maxTokens || 'N/A'}\n`;
        if (request.temperature !== undefined && request.temperature !== null) {
            output += `Temperature: ${request.temperature}\n`;
        }
        output += '\n';

        // System message
        if (request.system) {
            output += '--- SYSTEM ---\n';
            if (typeof request.system === 'string') {
                output += request.system + '\n';
            } else if (Array.isArray(request.system)) {
                request.system.forEach(block => {
                    if (block.type === 'text' && block.text) {
                        output += block.text + '\n';
                    }
                });
            }
            output += '\n';
        }

        // Messages
        if (request.messages && request.messages.length > 0) {
            output += '--- MESSAGES ---\n\n';
            request.messages.forEach((message, i) => {
                const role = message.role ? message.role.toUpperCase() : 'UNKNOWN';
                output += `[${role}]\n`;
                
                if (typeof message.content === 'string') {
                    output += `${message.content}\n`;
                } else if (Array.isArray(message.content)) {
                    message.content.forEach(block => {
                        if (block.type === 'text' && block.text) {
                            output += `${block.text}\n`;
                        }
                    });
                }
                output += '\n';
            });
        }

        // Thinking config
        if (request.thinking) {
            output += '--- THINKING CONFIG ---\n';
            output += JSON.stringify(request.thinking, null, 2) + '\n';
        }

        return output;
    }

    function syntaxHighlightJSON(obj) {
        try {
            console.log('syntaxHighlightJSON called with obj:', typeof obj);
            
            // Handle circular references and convert to string
            const seen = new WeakSet();
            let json = JSON.stringify(obj, (key, value) => {
                if (typeof value === 'object' && value !== null) {
                    if (seen.has(value)) {
                        return '[Circular Reference]';
                    }
                    seen.add(value);
                }
                return value;
            }, 2);
            
            console.log('JSON stringified, length:', json.length);
            
            if (!json || json === 'undefined' || json === 'null') {
                return '<span class="text-muted">No data available</span>';
            }
            
            json = json.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            
            const highlighted = json.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {
                let cls = 'json-number';
                if (/^"/.test(match)) {
                    if (/:$/.test(match)) {
                        cls = 'json-key';
                    } else {
                        cls = 'json-string';
                    }
                } else if (/true|false/.test(match)) {
                    cls = 'json-boolean';
                } else if (/null/.test(match)) {
                    cls = 'json-null';
                }
                return '<span class="' + cls + '">' + match + '</span>';
            });
            
            console.log('JSON highlighted, length:', highlighted.length);
            return highlighted;
        } catch (error) {
            console.error('Error in syntaxHighlightJSON:', error);
            return `<span class="text-danger">Error formatting JSON: ${error.message}</span>`;
        }
    }

    function copyToClipboard(text) {
        navigator.clipboard.writeText(text).catch(err => {
            console.error('Failed to copy:', err);
            // Fallback method
            const textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand('copy');
            document.body.removeChild(textarea);
        });
    }

    function showLoading(show) {
        if (show) {
            loadingSpinner.classList.add('active');
            processButton.disabled = true;
        } else {
            loadingSpinner.classList.remove('active');
            processButton.disabled = false;
        }
    }

    function showError(message) {
        errorMessage.textContent = message;
        errorMessage.classList.add('active');
        setTimeout(() => {
            errorMessage.classList.remove('active');
        }, 5000);
    }

    function showSuccess(message) {
        successMessage.textContent = message;
        successMessage.classList.add('active');
        setTimeout(() => {
            successMessage.classList.remove('active');
        }, 3000);
    }

    function hideMessages() {
        errorMessage.classList.remove('active');
        successMessage.classList.remove('active');
    }

    function displayLoadedContextData(loadedData) {
        // Display summary
        const summary = loadedData.summary;
        tokenCountsSummary.innerHTML = `
            <div class="alert alert-info mb-2">
                <strong>Loaded Context Data:</strong> 
                ${summary.totalItems} items loaded | 
                ${summary.itemsWithTokens} with token counts | 
                ${summary.itemsWithoutTokens} need counting | 
                <strong>${summary.totalTokens.toLocaleString()}</strong> total tokens
            </div>
        `;

        // Display items grouped by type
        const itemsByType = {};
        loadedData.items.forEach(item => {
            const typeKey = item.type || 'Unknown';
            if (!itemsByType[typeKey]) {
                itemsByType[typeKey] = [];
            }
            itemsByType[typeKey].push(item);
        });

        let html = '';
        for (const [type, items] of Object.entries(itemsByType)) {
            const typeTokens = items.filter(i => i.tokenCount).reduce((sum, i) => sum + i.tokenCount, 0);
            const typeItemsWithCount = items.filter(i => i.tokenCount).length;
            
            html += `
                <div class="state-item" style="grid-column: 1 / -1; border-left-color: #6c757d;">
                    <div style="font-weight: 600; color: #495057; margin-bottom: 8px;">
                        ${type} (${items.length} items, ${typeItemsWithCount} counted, ${typeTokens.toLocaleString()} tokens)
                    </div>
                    <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 8px;">
            `;
            
            items.forEach(item => {
                const tokenDisplay = item.tokenCount 
                    ? `<span class="badge bg-success">${item.tokenCount.toLocaleString()} tokens</span>`
                    : `<button class="btn btn-sm btn-outline-warning" onclick="countItemTokens(${item.id})">
                         <i class="fas fa-calculator"></i> Count
                       </button>`;
                
                const availabilityColor = {
                    'AlwaysOn': '#28a745',
                    'Semantic': '#007bff',
                    'Trigger': '#fd7e14',
                    'Manual': '#6c757d',
                    'Archive': '#dc3545'
                }[item.availability] || '#6c757d';
                
                html += `
                    <div style="padding: 8px; background: #f8f9fa; border-radius: 4px; border-left: 3px solid ${availabilityColor};">
                        <div style="font-size: 0.85em; font-weight: 500; margin-bottom: 4px;">${escapeHtml(item.name)}</div>
                        <div style="display: flex; justify-content: space-between; align-items: center; gap: 8px; font-size: 0.75em;">
                            <span class="badge" style="background-color: ${availabilityColor};">${item.availability}</span>
                            ${tokenDisplay}
                        </div>
                        <div style="font-size: 0.7em; color: #6c757d; margin-top: 4px;">
                            ${item.contentLength.toLocaleString()} chars
                        </div>
                    </div>
                `;
            });
            
            html += `
                    </div>
                </div>
            `;
        }

        tokenCountsGrid.innerHTML = html;
    }

    async function loadTokenCounts() {
        loadTokenCountsButton.disabled = true;
        loadTokenCountsButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Loading...';

        try {
            const response = await fetch(`${API_BASE}/api/conversation/debug/context-token-counts`);
            
            if (!response.ok) {
                throw new Error('Failed to load token counts');
            }

            const data = await response.json();
            tokenCountsData = data;
            
            displayTokenCounts(data);
            tokenCountsContainer.style.display = 'block';
            
        } catch (error) {
            console.error('Error loading token counts:', error);
            showError(`Error loading token counts: ${error.message}`);
        } finally {
            loadTokenCountsButton.disabled = false;
            loadTokenCountsButton.innerHTML = '<i class="fas fa-sync"></i> Load Token Counts';
        }
    }

    function displayTokenCounts(data) {
        // Display summary
        const summary = data.summary;
        tokenCountsSummary.innerHTML = `
            <div class="alert alert-info mb-2">
                <strong>All Enabled Context Data:</strong> 
                ${summary.totalItems} items total | 
                ${summary.itemsWithTokens} with token counts | 
                ${summary.itemsWithoutTokens} need counting | 
                <strong>${summary.totalTokens.toLocaleString()}</strong> total tokens
            </div>
        `;

        // Display items grouped by type
        const itemsByType = {};
        data.items.forEach(item => {
            const typeKey = item.type || 'Unknown';
            if (!itemsByType[typeKey]) {
                itemsByType[typeKey] = [];
            }
            itemsByType[typeKey].push(item);
        });

        let html = '';
        for (const [type, items] of Object.entries(itemsByType)) {
            const typeTokens = items.filter(i => i.tokenCount).reduce((sum, i) => sum + i.tokenCount, 0);
            const typeItemsWithCount = items.filter(i => i.tokenCount).length;
            
            html += `
                <div class="state-item" style="grid-column: 1 / -1; border-left-color: #6c757d;">
                    <div style="font-weight: 600; color: #495057; margin-bottom: 8px;">
                        ${type} (${items.length} items, ${typeItemsWithCount} counted, ${typeTokens.toLocaleString()} tokens)
                    </div>
                    <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 8px;">
            `;
            
            items.forEach(item => {
                const tokenDisplay = item.tokenCount 
                    ? `<span class="badge bg-success">${item.tokenCount.toLocaleString()} tokens</span>`
                    : `<button class="btn btn-sm btn-outline-warning" onclick="countItemTokens(${item.id})">
                         <i class="fas fa-calculator"></i> Count
                       </button>`;
                
                const availabilityColor = {
                    'AlwaysOn': '#28a745',
                    'Semantic': '#007bff',
                    'Trigger': '#fd7e14',
                    'Manual': '#6c757d',
                    'Archive': '#dc3545'
                }[item.availability] || '#6c757d';
                
                html += `
                    <div style="padding: 8px; background: #f8f9fa; border-radius: 4px; border-left: 3px solid ${availabilityColor};">
                        <div style="font-size: 0.85em; font-weight: 500; margin-bottom: 4px;">${escapeHtml(item.name)}</div>
                        <div style="display: flex; justify-content: space-between; align-items: center; gap: 8px; font-size: 0.75em;">
                            <span class="badge" style="background-color: ${availabilityColor};">${item.availability}</span>
                            ${tokenDisplay}
                        </div>
                        <div style="font-size: 0.7em; color: #6c757d; margin-top: 4px;">
                            ${item.contentLength.toLocaleString()} chars
                        </div>
                    </div>
                `;
            });
            
            html += `
                    </div>
                </div>
            `;
        }

        tokenCountsGrid.innerHTML = html;
    }

    // Make countItemTokens globally accessible
    window.countItemTokens = async function(id) {
        const button = event.target.closest('button');
        const originalHtml = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';

        try {
            const response = await fetch(`${API_BASE}/api/contextdata/${id}/count-tokens`, {
                method: 'POST'
            });

            if (!response.ok) {
                throw new Error('Failed to count tokens');
            }

            const result = await response.json();
            
            // Refresh the loaded data if we have current response
            if (currentResponse) {
                // Re-process to get updated data
                showSuccess(`Counted ${result.tokenCount} tokens for "${result.name}". Refresh to see updated counts.`);
            }
            
        } catch (error) {
            console.error('Error counting tokens:', error);
            showError(`Error counting tokens: ${error.message}`);
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    };
    
    // Helper function to format item names based on type
    // For Quote and PersonaVoiceSample, use the displayContent if available
    window.formatItemName = function(item) {
        // If the API provides displayContent, use it (truncated for display)
        if (item.displayContent && (item.type === 'Quote' || item.type === 'PersonaVoiceSample')) {
            // Truncate to a reasonable length for the UI
            const maxLength = 80;
            return item.displayContent.length > maxLength 
                ? item.displayContent.substring(0, maxLength) + '...'
                : item.displayContent;
        }
        
        // Fallback to the name field
        return item.name;
    };
});

(() => {
    "use strict";

    const container = document.getElementById("facilitymanager-content");
    if (!container) return;

    const LAST_PAGE_KEY = "fm-last-page";

    let requesterSearchTimer = null;
    let lastRequesterSearchValue = "";
    let keyTagSearchTimer = null;
    let lastKeyTagSearchValue = "";
    let smartComboOutsideClickBound = false;

    // =========================================================

    const qs = (selector, root = container) => root.querySelector(selector);
    const qsa = (selector, root = container) => Array.from(root.querySelectorAll(selector));

    const addKeyCombos = {
        building: null,
        floor: null,
        room: null,
        keyId: null
    };
    const updateKeyCombos = {
        building: null,
        floor: null,
        room: null,
        keyId: null
    };

    function initUpdateKeyComboboxes() {
        updateKeyCombos.building = createSmartCombobox({
            rootSelector: "#updateBuildingCombo",
            inputSelector: "#updateBuildingInput",
            hiddenSelector: "#updateBuildingValue",
            menuSelector: "#updateBuildingMenu",
            placeholder: "-- Select or type Building --",
            fetchOptions: async () => await getBuildings(),
            onValueChanged: async () => {
                setComboValue("#updateFloorValue", "#updateFloorInput", "");
                setComboValue("#updateRoomValue", "#updateRoomInput", "");

                updateKeyCombos.floor?.setDisabled(!getComboValue("#updateBuildingValue", "#updateBuildingInput"));
                updateKeyCombos.room?.setDisabled(true);
            }
        });

        updateKeyCombos.floor = createSmartCombobox({
            rootSelector: "#updateFloorCombo",
            inputSelector: "#updateFloorInput",
            hiddenSelector: "#updateFloorValue",
            menuSelector: "#updateFloorMenu",
            placeholder: "-- Select or type Floor --",
            fetchOptions: async () => {
                const building = getComboValue("#updateBuildingValue", "#updateBuildingInput");
                if (!building) return [];
                return await getFloors(building);
            },
            onValueChanged: async () => {
                setComboValue("#updateRoomValue", "#updateRoomInput", "");

                const building = getComboValue("#updateBuildingValue", "#updateBuildingInput");
                const floor = getComboValue("#updateFloorValue", "#updateFloorInput");

                updateKeyCombos.room?.setDisabled(!(building && floor));
            }
        });

        updateKeyCombos.room = createSmartCombobox({
            rootSelector: "#updateRoomCombo",
            inputSelector: "#updateRoomInput",
            hiddenSelector: "#updateRoomValue",
            menuSelector: "#updateRoomMenu",
            placeholder: "-- Select or type Room --",
            fetchOptions: async () => {
                const building = getComboValue("#updateBuildingValue", "#updateBuildingInput");
                const floor = getComboValue("#updateFloorValue", "#updateFloorInput");

                if (!building || !floor) return [];
                return await getRooms(building, floor);
            },
            onValueChanged: async () => { }
        });

        updateKeyCombos.keyId = createSmartCombobox({
            rootSelector: "#updateKeyIdCombo",
            inputSelector: "#updateKeyIdInput",
            hiddenSelector: "#updateKeyIdValue",
            menuSelector: "#updateKeyIdMenu",
            placeholder: "-- Select or type Key Number --",
            fetchOptions: async () => {
                const building = getComboValue("#updateBuildingValue", "#updateBuildingInput");
                const floor = getComboValue("#updateFloorValue", "#updateFloorInput");
                const room = getComboValue("#updateRoomValue", "#updateRoomInput");

                return await getKeyNumbers(building, floor, room);
            },
            onValueChanged: async () => { }
        });

        const hasBuilding = !!getComboValue("#updateBuildingValue", "#updateBuildingInput");
        const hasFloor = !!getComboValue("#updateFloorValue", "#updateFloorInput");

        updateKeyCombos.floor?.setDisabled(!hasBuilding);
        updateKeyCombos.room?.setDisabled(!(hasBuilding && hasFloor));
    }

    function normalizeItems(items) {
        return [...new Set((items || [])
            .map(x => (x || "").trim())
            .filter(Boolean))];
    }

    function getComboValue(hiddenSelector, inputSelector) {
        const hidden = qs(hiddenSelector);
        const input = qs(inputSelector);

        return (hidden?.value || input?.value || "").trim();
    }

    function setComboValue(hiddenSelector, inputSelector, value) {
        const safeValue = (value || "").trim();
        const hidden = qs(hiddenSelector);
        const input = qs(inputSelector);

        if (hidden) hidden.value = safeValue;
        if (input) input.value = safeValue;
    }

    function closeAllSmartCombos() {
        qsa(".smart-combobox-menu").forEach(menu => menu.classList.add("d-none"));
    }

    function createSmartCombobox({
        rootSelector,
        inputSelector,
        hiddenSelector,
        menuSelector,
        placeholder,
        fetchOptions,
        onValueChanged
    }) {
        const root = qs(rootSelector);
        const input = qs(inputSelector);
        const hidden = qs(hiddenSelector);
        const menu = qs(menuSelector);

        if (!root || !input || !hidden || !menu) return null;

        let allOptions = [];

        function setDisabled(disabled) {
            input.disabled = !!disabled;
            if (disabled) {
                input.value = "";
                hidden.value = "";
                menu.classList.add("d-none");
            }
        }

        function getValue() {
            return (hidden.value || input.value || "").trim();
        }

        function setValue(value, triggerChange = false) {
            const safeValue = (value || "").trim();
            input.value = safeValue;
            hidden.value = safeValue;

            if (triggerChange && typeof onValueChanged === "function") {
                onValueChanged(safeValue);
            }
        }

        async function refreshOptions() {
            const items = await fetchOptions();
            allOptions = normalizeItems(items);
            return allOptions;
        }

        function renderOptions(filterText = "") {
            const search = (filterText || "").trim().toLowerCase();

            const filtered = !search
                ? allOptions
                : allOptions.filter(x => x.toLowerCase().includes(search));

            if (filtered.length === 0) {
                menu.innerHTML = `<div class="smart-combobox-empty">No matching options</div>`;
                menu.classList.remove("d-none");
                return;
            }

            menu.innerHTML = filtered.map(item => `
            <div class="smart-combobox-item" data-value="${escapeHtml(item)}">
                ${escapeHtml(item)}
            </div>
        `).join("");

            menu.classList.remove("d-none");
        }

        async function open(showAll = true) {
            await refreshOptions();
            renderOptions(showAll ? "" : input.value);
        }

        input.placeholder = placeholder || "";

        input.addEventListener("focus", async () => {
            await open(true);
        });

        input.addEventListener("click", async () => {
            await open(true);
        });

        input.addEventListener("input", async () => {
            hidden.value = input.value.trim();

            if (typeof onValueChanged === "function") {
                await onValueChanged(hidden.value);
            }

            await refreshOptions();
            renderOptions(input.value);
        });

        input.addEventListener("keydown", async (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                event.stopPropagation();

                const typedValue = (input.value || "").trim();

                input.value = typedValue;
                hidden.value = typedValue;

                if (typeof onValueChanged === "function") {
                    await onValueChanged(typedValue);
                }

                closeAllSmartCombos();

                const end = typedValue.length;
                input.setSelectionRange?.(end, end);

                input.blur();

                return;
            }

            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();

                closeAllSmartCombos();
                input.blur();
            }
        });

        menu.addEventListener("click", async (event) => {
            const item = event.target.closest(".smart-combobox-item");
            if (!item) return;

            const value = (item.dataset.value || "").trim();
            setValue(value, false);
            menu.classList.add("d-none");

            if (typeof onValueChanged === "function") {
                await onValueChanged(value);
            }
        });

        return {
            root,
            input,
            hidden,
            menu,
            getValue,
            setValue,
            setDisabled,
            refreshOptions,
            open
        };
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function showMessage(selector, message) {
        const el = qs(selector);
        if (!el) return;

        el.textContent = message;
        el.classList.remove("d-none");
    }

    function hideMessage(selector) {
        const el = qs(selector);
        if (!el) return;

        el.textContent = "";
        el.classList.add("d-none");
    }

    function setSelectOptions(select, items, placeholder) {
        if (!select) return;

        select.innerHTML = `<option value="">${escapeHtml(placeholder)}</option>`;

        items.forEach(item => {
            const opt = document.createElement("option");
            opt.value = item;
            opt.textContent = item;
            select.appendChild(opt);
        });
    }

    function resetSelect(selector, placeholder, disabled = true) {
        const select = qs(selector);
        if (!select) return;

        setSelectOptions(select, [], placeholder);
        select.disabled = disabled;
    }

    async function getJson(url, fallback = null) {
        try {
            const response = await fetch(url);
            const data = await response.json().catch(() => fallback);

            return {
                ok: response.ok,
                status: response.status,
                data
            };
        } catch {
            return {
                ok: false,
                status: 0,
                data: fallback
            };
        }
    }

    async function postForm(url, body, token = null) {
        const headers = {};

        if (token) {
            headers.RequestVerificationToken = token;
        }

        const response = await fetch(url, {
            method: "POST",
            headers,
            body
        });

        const data = await response.json().catch(() => null);

        return {
            ok: response.ok,
            status: response.status,
            data
        };
    }

    async function fetchHtml(url) {
    return await fetch(url).then(r => r.text());
    }

    function formatDateToInput(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");

        return `${year}-${month}-${day}`;
    }

    function getAntiForgeryToken(formSelector) {
        return qs(`${formSelector} input[name="__RequestVerificationToken"]`)?.value;
    }

    // =========================================================

    function getModal(selector, options = undefined) {
        const modalEl = qs(selector);
        if (!modalEl) return null;

        return bootstrap.Modal.getOrCreateInstance(modalEl, options);
    }

    const getAddKeyModal = () =>
        getModal("#addKeyModal", {
            backdrop: true,
            keyboard: true,
            focus: true
        });

    const getUpdateKeyModal = () =>
        getModal("#updateKeyModal", {
            backdrop: true,
            keyboard: true,
            focus: true
        });

    const getViewKeyModal = () => getModal("#viewKeyModal");

    const getAssignmentViewModal = () => getModal("#viewAssignmentModal");

    // =========================================================

    function destroyDataTables() {
        ["#keysTable", "#assignmentsTable"].forEach(selector => {
            const table = qs(selector);

            if (table && $.fn.DataTable.isDataTable(table)) {
                $(table).DataTable().destroy(true);
            }
        });
    }
    function initDataTable(selector, entityName, nonOrderableTargets) {
        const table = qs(selector);
        if (!table) return;

        $(table).DataTable({
            pageLength: 10,
            lengthMenu: [5, 10, 25, 50],
            ordering: true,
            order: [],
            searching: true,
            responsive: false,
            autoWidth: false,
            info: false,
            pagingType: "simple_numbers",

            dom:
                "<'row mb-2'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
                "t" +
                "<'row mt-2'<'col-12'p>>",

            language: {
                search: "Search:",
                lengthMenu: "Show _MENU_ entries",
                emptyTable: `No ${entityName} found`,
                zeroRecords: `No matching ${entityName} found`,
                paginate: {
                    first: "First",
                    last: "Last",
                    next: "Next",
                    previous: "Previous"
                }
            },
            columnDefs: [
                { orderable: false, targets: nonOrderableTargets }
            ]
        });
    }


    function initKeyLookupDataTable() {
        const table = qs("#assignmentsTable");
        if (!table) return;

        $(table).DataTable({
            paging: false,
            lengthChange: false,
            ordering: true,
            order: [],
            searching: false,
            responsive: false,
            autoWidth: false,
            info: false,

            dom: '<"d-flex justify-content-between align-items-center mb-3"<"keylookup-search-slot">>rt',

            language: {
                emptyTable: "No assignments found",
                zeroRecords: "No matching assignments found"
            },

            columnDefs: [
                { orderable: false, targets: [10] }
            ],

            initComplete: function () {
                const wrapper = table.closest(".dataTables_wrapper");
                const slot = wrapper?.querySelector(".keylookup-search-slot");
                const searchInput = qs("#keyLookupSearch");

                if (slot && searchInput) {
                    const searchBlock = searchInput.parentElement;

                    if (searchBlock) {
                        searchBlock.classList.remove("mb-3");
                        searchBlock.classList.add("mb-0");
                        slot.appendChild(searchBlock);
                    } else {
                        slot.appendChild(searchInput);
                    }

                    searchInput.classList.add("w-auto");
                    searchInput.style.minWidth = "1375px";
                }
            }
        });
    }

    const initKeyDataTable = () => initDataTable("#keysTable", "keys", [6]);
    const initAssignmentDataTable = () => initDataTable("#assignmentsTable", "assignments", [9]);

    // =========================================================

    async function getBuildings() {
        const result = await getJson("FacilityManager/GetBuildings", []);
        return Array.isArray(result.data) ? result.data : [];
    }

    async function getFloors(building) {
        if (!building) return [];

        const url = `FacilityManager/GetFloors?building=${encodeURIComponent(building)}`;
        const result = await getJson(url, []);
        return Array.isArray(result.data) ? result.data : [];
    }

    async function getRooms(building, floor) {
        if (!building || !floor) return [];

        const url = `Administrator/GetRooms?building=${encodeURIComponent(building)}&floor=${encodeURIComponent(floor)}`;
        const result = await getJson(url, []);
        return Array.isArray(result.data) ? result.data : [];
    }

    async function getKeyNumbers(building = "", floor = "", room = "") {
        const url = new URL("Administrator/GetKeyNumbers", window.location.origin);

        if (building) url.searchParams.append("building", building);
        if (floor) url.searchParams.append("floor", floor);
        if (room) url.searchParams.append("room", room);

        const result = await getJson(url.toString(), []);
        return Array.isArray(result.data) ? result.data : [];
    }

    async function loadBuildingsInto(selector) {
        const select = qs(selector);
        if (!select) return;

        const buildings = await getBuildings();
        setSelectOptions(select, buildings, "-- Select Building --");
        select.disabled = false;
    }

    async function loadFloorsInto(selector, building) {
        const select = qs(selector);
        if (!select) return;

        resetSelect(selector, "-- Select Floor --", true);

        if (!building) return;

        const floors = await getFloors(building);
        setSelectOptions(select, floors, "-- Select Floor --");
        select.disabled = false;
    }

    async function loadRoomsInto(selector, building, floor) {
        const select = qs(selector);
        if (!select) return;

        resetSelect(selector, "-- Select Room --", true);

        if (!building || !floor) return;

        const rooms = await getRooms(building, floor);
        setSelectOptions(select, rooms, "-- Select Room --");
        select.disabled = false;
    }

    function initAddKeyComboboxes() {
        addKeyCombos.building = createSmartCombobox({
            rootSelector: "#buildingCombo",
            inputSelector: "#buildingInput",
            hiddenSelector: "#buildingValue",
            menuSelector: "#buildingMenu",
            placeholder: "-- Select or type Building --",
            fetchOptions: async () => await getBuildings(),
            onValueChanged: async () => {
                setComboValue("#floorValue", "#floorInput", "");
                setComboValue("#roomValue", "#roomInput", "");

                addKeyCombos.floor?.setDisabled(!getComboValue("#buildingValue", "#buildingInput"));
                addKeyCombos.room?.setDisabled(true);

                renderIndividualKeyInputs(1, 1);
                await checkAddKeyExistsAndUpdateUI();
            }
        });

        addKeyCombos.floor = createSmartCombobox({
            rootSelector: "#floorCombo",
            inputSelector: "#floorInput",
            hiddenSelector: "#floorValue",
            menuSelector: "#floorMenu",
            placeholder: "-- Select or type Floor --",
            fetchOptions: async () => {
                const building = getComboValue("#buildingValue", "#buildingInput");
                if (!building) return [];
                return await getFloors(building);
            },
            onValueChanged: async () => {
                setComboValue("#roomValue", "#roomInput", "");

                const building = getComboValue("#buildingValue", "#buildingInput");
                const floor = getComboValue("#floorValue", "#floorInput");

                addKeyCombos.room?.setDisabled(!(building && floor));

                renderIndividualKeyInputs(1, 1);
                await checkAddKeyExistsAndUpdateUI();
            }
        });

        addKeyCombos.room = createSmartCombobox({
            rootSelector: "#roomCombo",
            inputSelector: "#roomInput",
            hiddenSelector: "#roomValue",
            menuSelector: "#roomMenu",
            placeholder: "-- Select or type Room --",
            fetchOptions: async () => {
                const building = getComboValue("#buildingValue", "#buildingInput");
                const floor = getComboValue("#floorValue", "#floorInput");

                if (!building || !floor) return [];
                return await getRooms(building, floor);
            },
            onValueChanged: async () => {
                await refreshIndividualKeyInputs();
                await checkAddKeyExistsAndUpdateUI();
            }
        });

        addKeyCombos.keyId = createSmartCombobox({
            rootSelector: "#keyIdCombo",
            inputSelector: "#keyIdInput",
            hiddenSelector: "#keyIdValue",
            menuSelector: "#keyIdMenu",
            placeholder: "-- Select or type Key Number --",
            fetchOptions: async () => {
                const building = getComboValue("#buildingValue", "#buildingInput");
                const floor = getComboValue("#floorValue", "#floorInput");
                const room = getComboValue("#roomValue", "#roomInput");

                return await getKeyNumbers(building, floor, room);
            },
            onValueChanged: async () => { await checkAddKeyExistsAndUpdateUI(); }
        });

        const hasBuilding = !!getComboValue("#buildingValue", "#buildingInput");
        const hasFloor = !!getComboValue("#floorValue", "#floorInput");

        addKeyCombos.floor?.setDisabled(!hasBuilding);
        addKeyCombos.room?.setDisabled(!(hasBuilding && hasFloor));

        if (!smartComboOutsideClickBound) {
            document.addEventListener("click", (event) => {
                if (!event.target.closest(".smart-combobox")) {
                    closeAllSmartCombos();
                }
            });

            smartComboOutsideClickBound = true;
        }
    }

    async function loadKeyLookupPage(search = "") {
        destroyDataTables();

        container.innerHTML = "";

        const trimmed = (search || "").trim();
        const query = trimmed
            ? `?search=${encodeURIComponent(trimmed)}`
            : "";

        const html = await fetch(`FacilityManager/KeyLookupGrid${query}`).then(r => r.text());
        container.innerHTML = html;

        initKeyLookupDataTable();

        const searchInput = qs("#keyLookupSearch");
        if (searchInput) {
            searchInput.value = search;
            searchInput.focus();

            const len = search.length;
            searchInput.setSelectionRange?.(len, len);
        }
    }

    // =========================================================

    async function loadGrid(url, initCallback) {
    destroyDataTables();
    container.innerHTML = "";

    const html = await fetchHtml(url);
    container.innerHTML = html;

    initCallback?.();
    }

    const loadKeyGrid = () =>
        loadGrid("FacilityManager/AllKeysGrid", () => {
            initKeyDataTable();
            initAddKeyComboboxes();
            initUpdateKeyComboboxes();
        });

    const loadAssignmentGrid = () =>
        loadGrid("FacilityManager/KeyManagerGrid", initAssignmentDataTable);

    const loadIssuedAndOverdueGrid = () =>
        loadGrid("FacilityManager/IssuedandOverdueGrid", initAssignmentDataTable);

    const loadKeyLookupGrid = () =>
        loadGrid("FacilityManager/KeyLookupGrid", initKeyLookupDataTable);

    async function loadAssignmentForm() {
        const html = await fetchHtml("FacilityManager/CreateAssignmentForm");

        container.innerHTML = html;

        syncDelegateNameState();
        syncAssignmentTypeState();
        calculateDueDate();
    }

    function setActiveMenuButton(activeButton) {
        document.querySelectorAll(".menu-link")
            .forEach(button => button.classList.remove("active"));

        activeButton?.classList.add("active");
    }

    async function openPage(pageName) {
        switch (pageName) {
            case "keys":
                await loadKeyGrid();
                setActiveMenuButton(document.getElementById("btnAllKeys"));
                break;

            case "create-assignment":
                await loadAssignmentForm();
                setActiveMenuButton(document.getElementById("btnCreateAssignment"));
                break;

            case "issued-overdue":
                await loadIssuedAndOverdueGrid();
                setActiveMenuButton(document.getElementById("btnIssuedandOverdueAssignments"));
                break;

            case "key-lookup":
                await loadKeyLookupPage("");
                setActiveMenuButton(document.getElementById("btnKeyLookup"));
                break;

            case "assignments":
            default:
                pageName = "assignments";
                await loadAssignmentGrid();
                setActiveMenuButton(document.getElementById("btnAllAssignments"));
                break;
        }

        sessionStorage.setItem(LAST_PAGE_KEY, pageName);
    }

    // =========================================================

    async function getNextKeyNumberForSelection() {
        const building = getComboValue("#buildingValue", "#buildingInput");
        const floor = getComboValue("#floorValue", "#floorInput");
        const room = getComboValue("#roomValue", "#roomInput");

        if (!building || !floor || !room) return 1;

        const url = `FacilityManager/GetNextKeyNumber?building=${encodeURIComponent(building)}&floor=${encodeURIComponent(floor)}&room=${encodeURIComponent(room)}`;
        const result = await getJson(url, null);

        if (!result.ok || !result.data?.ok) return 1;

        return parseInt(result.data.nextKeyNumber, 10) || 1;
    }

    async function refreshIndividualKeyInputs() {
        const validation = validateNumberOfKeysValue();

        if (!validation.isValid) return;

        const startNumber = await getNextKeyNumberForSelection();
        renderIndividualKeyInputs(validation.count, startNumber);
    }

    function renderIndividualKeyInputs(count, startNumber = 1) {
        const host = qs("#individual-key-fields");
        if (!host) return;

        let safeCount = parseInt(count, 10);

        if (isNaN(safeCount) || safeCount < 1) safeCount = 1;

        const existingValues = qsa('input[name^="TagNumbers["]', host)
            .map(input => input.value);

        host.innerHTML = "";

        for (let i = 0; i < safeCount; i++) {
            const keyLabelNumber = startNumber + i;

            const col = document.createElement("div");
            col.className = "col-md-6";
            col.innerHTML = `
                <label class="form-label mb-1">Key Tag ${keyLabelNumber}</label>
                <input name="TagNumbers[${i}]" class="form-control" value="${escapeHtml(existingValues[i] || "")}" />
            `;

            host.appendChild(col);
        }
    }

    function setAddKeyModeForExistingRecord(exists) {
        const host = qs("#individual-key-fields");
        const numberOfKeysInput = qs("#numberOfKeysInput");
        const submitBtn = qs("#add-key-form button[type='submit']");

        if (exists) {
            if (host) host.innerHTML = "";
            if (numberOfKeysInput) {
                numberOfKeysInput.value = "";
                numberOfKeysInput.disabled = true;
            }
            if (submitBtn) submitBtn.disabled = true;

            showMessage("#add-key-msg", "A record already exists for that key. Use the update button to make changes to that key.");
            return;
        }

        if (numberOfKeysInput) {
            numberOfKeysInput.disabled = false;
            if (!numberOfKeysInput.value) numberOfKeysInput.value = "1";
        }

        if (submitBtn) submitBtn.disabled = false;

        hideMessage("#add-key-msg");
        try {
            const validation = validateNumberOfKeysValue();
            if (validation.isValid) renderIndividualKeyInputs(validation.count, 1);
            else renderIndividualKeyInputs(1, 1);
        } catch (e) {
        }
    }

    async function checkAddKeyExistsAndUpdateUI() {
        const building = getComboValue("#buildingValue", "#buildingInput");
        const floor = getComboValue("#floorValue", "#floorInput");
        const room = getComboValue("#roomValue", "#roomInput");
        const keyId = getComboValue("#keyIdValue", "#keyIdInput");

        if (!building || !floor || !room || !keyId) {
            setAddKeyModeForExistingRecord(false);
            return;
        }

        const url = new URL("FacilityManager/DoesKeyRecordExist", window.location.origin);
        url.searchParams.append("building", building);
        url.searchParams.append("floor", floor);
        url.searchParams.append("room", room);
        url.searchParams.append("keyId", keyId);

        const result = await getJson(url.toString(), null);
        if (result && result.ok && result.data && typeof result.data.exists !== "undefined") {
            setAddKeyModeForExistingRecord(!!result.data.exists);
            return;
        }

        setAddKeyModeForExistingRecord(false);
    }

    function clearIndividualKeyInputs() {
        const host = qs("#individual-key-fields");
        if (!host) return;

        host.innerHTML = "";
    }

    function validateNumberOfKeysValue() {
        const input = qs("#numberOfKeysInput");
        if (!input) {
            return { isValid: false, count: 1 };
        }

        const raw = (input.value || "").trim();

        if (raw === "") {
            clearIndividualKeyInputs();
            hideMessage("#number-of-keys-msg");

            return { isValid: false, count: 1 };
        }

        const count = parseInt(raw, 10);

        if (isNaN(count) || count < 1) {
            clearIndividualKeyInputs();
            hideMessage("#number-of-keys-msg");

            return { isValid: false, count: 1 };
        }

        if (count > 10) {
            clearIndividualKeyInputs();
            showMessage("#number-of-keys-msg", "You can only add 10 keys at a time.");

            return { isValid: false, count };
        }

        hideMessage("#number-of-keys-msg");

        return { isValid: true, count };
    }

    function resetAddKeyModal() {
        qs("#add-key-form")?.reset();

        setComboValue("#buildingValue", "#buildingInput", "");
        setComboValue("#floorValue", "#floorInput", "");
        setComboValue("#roomValue", "#roomInput", "");
        setComboValue("#keyIdValue", "#keyIdInput", "");

        addKeyCombos.floor?.setDisabled(true);
        addKeyCombos.room?.setDisabled(true);

        const numberOfKeysInput = qs("#numberOfKeysInput");
        if (numberOfKeysInput) numberOfKeysInput.value = "1";

        setAddKeyModalTitle("Add new key");
        renderIndividualKeyInputs(1, 1);
        setAddKeyModeForExistingRecord(false);
        hideMessage("#number-of-keys-msg");
        hideMessage("#add-key-msg");
        closeAllSmartCombos();
    }

    function setAddKeyModalTitle(title) {
        const modalTitle = qs("#addKeyModalTitle");
        if (modalTitle) {
            modalTitle.textContent = title;
        }
    }

    async function openAddKeyModalForAdd() {
        resetAddKeyModal();
        setAddKeyModalTitle("Add new key");
        await addKeyCombos.building?.refreshOptions();
        await addKeyCombos.keyId?.refreshOptions();
        renderIndividualKeyInputs(1, 1);
        await checkAddKeyExistsAndUpdateUI();
        getAddKeyModal()?.show();
    }

    function resetUpdateKeyModal() {
        qs("#update-key-form")?.reset();

        qs("#updateKeyRecordId").value = "";

        setComboValue("#updateBuildingValue", "#updateBuildingInput", "");
        setComboValue("#updateFloorValue", "#updateFloorInput", "");
        setComboValue("#updateRoomValue", "#updateRoomInput", "");
        setComboValue("#updateKeyIdValue", "#updateKeyIdInput", "");

        updateKeyCombos.floor?.setDisabled(true);
        updateKeyCombos.room?.setDisabled(true);

        const numberInput = qs("#updateNumberOfKeysInput");
        if (numberInput) numberInput.value = "0";

        const existingHost = qs("#update-existing-key-fields");
        if (existingHost) existingHost.innerHTML = "";

        renderUpdateNewKeyInputs(0);

        hideMessage("#update-key-msg");
        hideMessage("#update-number-of-keys-msg");
        closeAllSmartCombos();
    }

    async function openUpdateKeyModal(button) {
        if (!button) return;

        const id = (button.dataset.id || "").trim();
        if (!id) return;

        resetUpdateKeyModal();

        const result = await getJson(`FacilityManager/GetKeyForUpdate?id=${encodeURIComponent(id)}`, null);

        if (!result.ok || !result.data?.ok) {
            alert(result.data?.message || "Failed to load key for update.");
            return;
        }

        const data = result.data;

        qs("#updateKeyRecordId").value = data.id || "";

        setComboValue("#updateBuildingValue", "#updateBuildingInput", data.building || "");
        updateKeyCombos.floor?.setDisabled(!(data.building || ""));
        setComboValue("#updateFloorValue", "#updateFloorInput", data.floor || "");
        updateKeyCombos.room?.setDisabled(!((data.building || "") && (data.floor || "")));
        setComboValue("#updateRoomValue", "#updateRoomInput", data.room || "");
        setComboValue("#updateKeyIdValue", "#updateKeyIdInput", data.keyId || "");

        await updateKeyCombos.building?.refreshOptions();
        await updateKeyCombos.floor?.refreshOptions();
        await updateKeyCombos.room?.refreshOptions();
        await updateKeyCombos.keyId?.refreshOptions();

        renderUpdateExistingKeyInputs(data.existingKeys || []);
        renderUpdateNewKeyInputs(0, (data.existingKeys || []).length + 1);

        getUpdateKeyModal()?.show();
    }

    function getStatusBadgeHtml(status) {
        const safeStatus = escapeHtml(status || "Free");
        const normalizedStatus = (status || "Free").toLowerCase();

        let badgeClass = "bg-success";

        if (normalizedStatus === "issued") {
            badgeClass = "bg-primary";
        } else if (normalizedStatus === "overdue") {
            badgeClass = "bg-danger";
        } else if (normalizedStatus === "free") {
            badgeClass = "bg-success";
        } else {
            badgeClass = "bg-secondary";
        }

        return `
        <span class="${badgeClass} text-white text-center"
              style="border-radius:20%; padding:2px 5px; display:inline-block; min-width:80px;">
            ${safeStatus}
        </span>
    `;
    }

    function renderUpdateExistingKeyInputs(keys) {
        const host = qs("#update-existing-key-fields");
        if (!host) return;

        if (!Array.isArray(keys) || keys.length === 0) {
            host.innerHTML = `<div class="text-muted small">No keys found.</div>`;
            return;
        }

        host.innerHTML = keys.map((key, index) => {
            const status = (key.status || "Free").toLowerCase();
            const isLocked = status === "issued" || status === "overdue";
            const tagValue = escapeHtml(key.tagNumber || "");

            const visibleInput = isLocked
                ? `<input class="form-control" value="${tagValue}" disabled />`
                : `<input name="ExistingKeys[${index}].TagNumber" class="form-control" value="${tagValue}" />`;

            const hiddenTagInput = isLocked
                ? `<input type="hidden" name="ExistingKeys[${index}].TagNumber" value="${tagValue}" />`
                : "";

            return `
        <div class="col-md-6 border rounded p-2 mb-2">
            <input type="hidden" name="ExistingKeys[${index}].Id" value="${escapeHtml(key.id)}" />
            <input type="hidden" name="ExistingKeys[${index}].Status" value="${escapeHtml(key.status || "Free")}" />

            <div class="d-flex justify-content-between align-items-center mb-2">
                <label class="form-label mb-0">
                    <strong>Key Tag ${index + 1}</strong>
                </label>
                ${getStatusBadgeHtml(key.status)}
            </div>

            ${visibleInput}
            ${hiddenTagInput}
        </div>
        `;
        }).join("");
    }

    function renderUpdateNewKeyInputs(count, startNumber) {
        const host = qs("#update-new-key-fields");
        if (!host) return;

        let safeCount = parseInt(count, 10);
        if (isNaN(safeCount) || safeCount < 0) safeCount = 0;

        let start = 1;
        if (typeof startNumber === "number" && !isNaN(startNumber) && startNumber >= 1) {
            start = startNumber;
        } else {
            const existingHost = qs("#update-existing-key-fields");
            if (existingHost) {
                const existingInputs = Array.from(existingHost.querySelectorAll('input[name^="ExistingKeys["]'));
                const existingCount = Math.floor(existingInputs.length / 3);
                start = existingCount + 1;
            }
        }

        const existingValues = qsa('input[name^="NewTagNumbers["]', host).map(input => input.value);

        host.innerHTML = "";

        for (let i = 0; i < safeCount; i++) {
            const labelNumber = start + i;
            const col = document.createElement("div");
            col.className = "col-md-6";
            col.innerHTML = `
            <label class="form-label mb-1">Key Tag ${labelNumber}</label>
            <input name="NewTagNumbers[${i}]" class="form-control" value="${escapeHtml(existingValues[i] || "")}" />
        `;
            host.appendChild(col);
        }
    }

    async function handleUpdateNumberOfKeysInput(input) {
        let raw = input.value;

        if (raw === "" || raw === null || raw === undefined) {
            hideMessage("#update-number-of-keys-msg");
            renderUpdateNewKeyInputs(0);
            return;
        }

        let value = parseInt(raw, 10);

        if (isNaN(value) || value < 0) {
            value = 0;
        }

        const MAX = 10;
        if (value > MAX) {
            value = MAX;
        }

        input.value = value.toString();
        renderUpdateNewKeyInputs(value);
    }

    async function handleUpdateKeySubmit(form) {
        const formData = new FormData(form);

        const response = await fetch(form.action, {
            method: "POST",
            body: formData
        });

        const data = await response.json().catch(() => null);

        if (!response.ok) {
            showMessage("#update-key-msg", data?.message || "Something went wrong.");
            return;
        }

        getUpdateKeyModal()?.hide();
        await openPage("keys");

        if (data?.message) {
            alert(data.message);
        }
    }

    function resetViewKeySelections() {
        qsa(".individual-key-checkbox").forEach(checkbox => {
            checkbox.checked = false;
        });

        const selectAll = qs("#selectAllIndividualKeys");
        if (selectAll) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
        }

        syncSelectAllIndividualKeysState();
    }

    function syncSelectAllIndividualKeysState() {
        const selectAll = qs("#selectAllIndividualKeys");
        if (!selectAll) return;

        const checkboxes = qsa(".individual-key-checkbox");

        if (checkboxes.length === 0) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
            selectAll.disabled = true;
            return;
        }

        selectAll.disabled = false;

        const checkedCount = checkboxes.filter(cb => cb.checked).length;

        selectAll.checked = checkedCount === checkboxes.length;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < checkboxes.length;
    }

    function setAllIndividualKeyCheckboxes(checked) {
        qsa(".individual-key-checkbox").forEach(checkbox => {
            checkbox.checked = checked;
        });

        syncSelectAllIndividualKeysState();
    }

    function renderIndividualKeysForView(keys) {
        const host = qs("#view-individual-keys");
        if (!host) return;

        const selectAll = qs("#selectAllIndividualKeys");

        if (selectAll) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
            selectAll.disabled = true;
        }

        if (!Array.isArray(keys) || keys.length === 0) {
            host.innerHTML = `<div class="text-muted small">No individual keys found.</div>`;
            syncSelectAllIndividualKeysState();
            return;
        }

        host.innerHTML = keys.map(key => `
            <div class="border rounded p-2 mb-2 d-flex justify-content-between align-items-center">
                <div>
                    <strong>${escapeHtml(key.label)}:</strong> ${escapeHtml(key.tagNumber || "-")}
                </div>

                <div class="d-flex align-items-center gap-3">
                    ${getStatusBadgeHtml(key.status)}

                    <div class="form-check mb-0">
                        <input class="form-check-input individual-key-checkbox"
                               type="checkbox"
                               value="${escapeHtml(key.id)}"
                               id="individual-key-${escapeHtml(key.id)}">

                        <label class="form-check-label" for="individual-key-${escapeHtml(key.id)}">
                            Delete
                        </label>
                    </div>
                </div>
            </div>
        `).join("");

        syncSelectAllIndividualKeysState();
    }

    async function handleViewKey(button) {
        const id = button.dataset.id;

        hideMessage("#view-key-msg");

        const result = await getJson(`FacilityManager/GetKeyDetails?id=${encodeURIComponent(id)}`, null);

        if (!result.ok || !result.data?.ok) {
            showMessage("#view-key-msg", result.data?.message || "Failed to load key details.");
            return;
        }

        const data = result.data;

        qs("#view-keyid").textContent = data.keyId || "";
        qs("#view-keycode").textContent = data.keyCode || "";
        qs("#view-building").textContent = data.building || "";
        qs("#view-floor").textContent = data.floor || "";
        qs("#view-room").textContent = data.room || "";

        renderIndividualKeysForView(data.individualKeys || []);

        resetViewKeySelections();
        hideMessage("#view-key-msg");

        const deleteBtn = qs("#btn-delete-key");
        if (deleteBtn) deleteBtn.dataset.id = data.id;

        getViewKeyModal()?.show();
    }

    async function handleDeleteKey(button) {
        const id = button.dataset.id;
        if (!id) return;

        hideMessage("#view-key-msg");

        const selectedIndividualKeyIds = Array.from(
            document.querySelectorAll("#viewKeyModal .individual-key-checkbox:checked")
        ).map(input => input.value);

        if (selectedIndividualKeyIds.length === 0) {
            showMessage("#view-key-msg", "You have not made any selection for deleting.");
            return;
        }

        let confirmMessage = selectedIndividualKeyIds.length > 1
            ? "Are you sure you want to delete these keys?"
            : "Are you sure you want to delete this key?";

        if (!window.confirm(confirmMessage)) return;

        const token = getAntiForgeryToken("#view-key-form");

        const body = new URLSearchParams();
        body.append("id", id);

        const selectAllChecked =
            document.querySelector("#selectAllIndividualKeys")?.checked || false;
        body.append("deleteAll", selectAllChecked ? "true" : "false");

        selectedIndividualKeyIds.forEach((keyId, index) => {
            body.append(`individualKeyIds[${index}]`, keyId);
        });

        const result = await postForm("FacilityManager/DeleteKey", body, token);

        if (!result.ok) {
            showMessage("#view-key-msg", result.data?.message || "Failed to delete key.");
            return;
        }

        getViewKeyModal()?.hide();
        await openPage("keys");
    }

    async function handleAddKeySubmit(form) {
        const numberOfKeys = parseInt(qs("#numberOfKeysInput")?.value, 10);

        if (!isNaN(numberOfKeys) && numberOfKeys > 10) {
            clearIndividualKeyInputs();
            showMessage("#number-of-keys-msg", "You can only add 10 keys at a time.");
            return;
        }

        const formData = new FormData(form);

        const response = await fetch(form.action, {
            method: "POST",
            body: formData
        });

        const data = await response.json().catch(() => null);

        if (!response.ok) {
            showMessage("#add-key-msg", data?.message || "Something went wrong.");
            return;
        }

        getAddKeyModal()?.hide();

        await openPage("keys");

        if (data?.message) {
            alert(data.message);
        }
    }

    // =========================================================

    function clearAssignmentLocationFields() {
        const floorInput = qs("#FloorNumber");
        const buildingInput = qs("#Building");
        const roomInput = qs("#RoomNumber");

        if (floorInput) floorInput.value = "";
        if (buildingInput) buildingInput.value = "";
        if (roomInput) roomInput.value = "";
    }

    async function searchKeyTag(tagNumber) {
        const tagInput = qs("#TagNumber");
        const floorInput = qs("#FloorNumber");
        const buildingInput = qs("#Building");
        const roomInput = qs("#RoomNumber");

        if (!tagInput || !floorInput || !buildingInput || !roomInput) return;

        tagNumber = (tagNumber || tagInput.value || "").trim();

        if (tagNumber.length === 0) {
            clearAssignmentLocationFields();
            return;
        }

        const result = await getJson(`FacilityManager/SearchKeyTag?tagNumber=${encodeURIComponent(tagNumber)}`, null);
        const currentTagValue = (tagInput.value || "").trim();

        if (currentTagValue !== tagNumber) return;

        if (!result.ok || !result.data?.ok) {
            clearAssignmentLocationFields();
            return;
        }

        floorInput.value = result.data.floor || "";
        buildingInput.value = result.data.building || "";
        roomInput.value = result.data.room || "";
    }

    function handleKeyTagInput(input) {
        const value = (input.value || "").trim();

        if (keyTagSearchTimer) {
            clearTimeout(keyTagSearchTimer);
        }

        if (value.length === 0) {
            lastKeyTagSearchValue = "";
            clearAssignmentLocationFields();
            return;
        }

        if (value === lastKeyTagSearchValue) return;

        keyTagSearchTimer = setTimeout(async () => {
            lastKeyTagSearchValue = value;
            await searchKeyTag(value);
        }, 300);
    }

    function syncDelegateNameState() {
        const delegateRadio = qs("#delegateDelegate");
        const delegateNameInput = qs("#delegateName");

        if (!delegateRadio || !delegateNameInput) return;

        if (delegateRadio.checked) {
            delegateNameInput.disabled = false;
            delegateNameInput.readOnly = false;
            delegateNameInput.focus();
        } else {
            delegateNameInput.value = "";
            delegateNameInput.disabled = true;
            delegateNameInput.readOnly = true;
        }
    }

    function calculateDueDate() {
        const temporaryRadio = qs("#assignmentTemporary");
        const dateRequestedInput = qs("#DateRequested");
        const durationInput = qs("#Duration");
        const dueDateInput = qs("#DueDate");

        if (!temporaryRadio?.checked || !dateRequestedInput || !durationInput || !dueDateInput) return;

        const dateRequestedValue = dateRequestedInput.value;
        const durationValue = parseInt(durationInput.value, 10);

        if (!dateRequestedValue || isNaN(durationValue) || durationValue <= 0) {
            dueDateInput.value = "";
            return;
        }

        const [year, month, day] = dateRequestedValue.split("-").map(Number);
        const baseDate = new Date(year, month - 1, day);

        baseDate.setDate(baseDate.getDate() + durationValue);

        dueDateInput.value = formatDateToInput(baseDate);
    }

    function syncAssignmentTypeState() {
        const temporaryRadio = qs("#assignmentTemporary");

        const dateRequestedInput = qs("#DateRequested");
        const durationInput = qs("#Duration");
        const dueDateInput = qs("#DueDate");

        if (!temporaryRadio || !dateRequestedInput || !durationInput || !dueDateInput) return;

        const today = formatDateToInput(new Date());

        dateRequestedInput.disabled = false;
        dateRequestedInput.value = today;
        dateRequestedInput.min = today;
        dateRequestedInput.max = today;
        dateRequestedInput.readOnly = true;

        if (temporaryRadio.checked) {
            durationInput.disabled = false;
            dueDateInput.disabled = false;

            durationInput.readOnly = false;
            dueDateInput.readOnly = true;

            calculateDueDate();
            return;
        }

        durationInput.value = "";
        dueDateInput.value = "";

        durationInput.disabled = true;
        dueDateInput.disabled = true;

        durationInput.readOnly = true;
        dueDateInput.readOnly = true;
    }

    function clearRequesterActiveAssignments() {
        const section = qs("#requester-active-assignments-section");
        const tbody = qs("#requester-active-assignments-body");

        if (tbody) {
            tbody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center text-muted">
                    No current assignments found.
                </td>
            </tr>
        `;
        }

        if (section) {
            section.classList.add("d-none");
        }
    }

    function getAssignmentStatusBadgeHtml(status) {
        const safeStatus = escapeHtml(status || "");
        const normalizedStatus = (status || "").toLowerCase();

        let badgeClass = "bg-secondary";

        if (normalizedStatus === "issued") {
            badgeClass = "bg-primary";
        } else if (normalizedStatus === "overdue") {
            badgeClass = "bg-danger";
        }

        return `
        <span class="badge ${badgeClass}">
            ${safeStatus}
        </span>
    `;
    }

    function renderRequesterActiveAssignments(assignments) {
        const section = qs("#requester-active-assignments-section");
        const tbody = qs("#requester-active-assignments-body");

        if (!section || !tbody) return;

        if (!Array.isArray(assignments) || assignments.length === 0) {
            tbody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center text-muted">
                    No current assignments found.
                </td>
            </tr>
        `;

            section.classList.remove("d-none");
            return;
        }

        tbody.innerHTML = assignments.map(a => `
        <tr>
            <td>${escapeHtml(a.building || "-")}</td>
            <td>${escapeHtml(a.floor || "-")}</td>
            <td>${escapeHtml(a.room || "-")}</td>
            <td>${escapeHtml(a.assignmentType || "-")}</td>
            <td>${escapeHtml(a.tagNumber || "-")}</td>
            <td>${getAssignmentStatusBadgeHtml(a.status || "-")}</td>
        </tr>
    `).join("");

        section.classList.remove("d-none");
    }

    async function loadRequesterActiveAssignments(igg) {
        igg = (igg || "").trim();

        if (!igg) {
            clearRequesterActiveAssignments();
            return;
        }

        const result = await getJson(`FacilityManager/GetRequesterActiveAssignments?igg=${encodeURIComponent(igg)}`, null);

        if (!result.ok || !result.data?.ok) {
            clearRequesterActiveAssignments();
            return;
        }

        renderRequesterActiveAssignments(result.data.assignments || []);
    }

    function clearRequesterFields(clearMessage = true) {
        const fields = ["#RequesterName", "#Department", "#Division"];

        fields.forEach(selector => {
            const field = qs(selector);
            if (field) field.value = "";
        });

        clearRequesterActiveAssignments();
    }

    async function searchRequester(igg) {
        const iggInput = qs("#RequesterIGG");
        const requesterName = qs("#RequesterName");
        const department = qs("#Department");
        const division = qs("#Division");

        if (!iggInput || !requesterName || !department || !division) return;

        igg = (igg || iggInput.value || "").trim();

        hideMessage("#requester-search-msg");

        if (igg.length === 0) {
            clearRequesterFields(false);
            return;
        }

        const result = await getJson(`FacilityManager/SearchRequester?igg=${encodeURIComponent(igg)}`, null);
        const currentIgg = (iggInput.value || "").trim();

        if (currentIgg !== igg) return;

        if (!result.ok || !result.data?.ok) {
            clearRequesterFields(false);
            return;
        }

        requesterName.value = result.data.name || "";
        department.value = result.data.department || "";
        division.value = result.data.division || "";

        await loadRequesterActiveAssignments(result.data.igg || igg);
    }

    async function handleViewAssignment(button) {
        const id = button.dataset.id;
        if (!id) return;

        const result = await getJson(`FacilityManager/GetAssignmentDetails?id=${encodeURIComponent(id)}`, null);

        if (!result.ok || !result.data?.ok) {
            alert(result.data?.message || "Failed to load assignment details.");
            return;
        }

        const data = result.data;

        const setText = (selector, value) => {
            const el = qs(selector);
            if (el) el.textContent = value || "";
        };

        const viewId = qs("#view-id");
        if (viewId) viewId.value = data.id || "";

        setText("#view-requesterigg", data.requesterIGG);
        setText("#view-requestername", data.requesterName);
        setText("#view-department", data.department);
        setText("#view-division", data.division);
        setText("#view-collectortype", data.collectorType);
        setText("#view-tagnumber", data.tagNumber);
        setText("#view-roomnumber", data.roomNumber);
        setText("#view-floornumber", data.floorNumber);
        setText("#view-keyid", data.keyid);
        setText("#view-assignmenttype", data.assignmentType);
        setText("#view-status", data.status);

        const delegateRow = qs("#view-delegate-row");
        const hasDelegate = !!(data.delegateName && data.delegateName.trim());

        if (delegateRow) {
            delegateRow.classList.toggle("d-none", !hasDelegate);
        }

        setText("#view-delegatename", hasDelegate ? data.delegateName : "");

        const timelineSection = qs("#view-timeline-section");
        const isPermanent = (data.assignmentType || "").trim().toLowerCase() === "permanent";

        const durationRow = qs("#view-duration")?.closest(".row, .mb-2, .form-group, tr, div");
        const dueDateRow = qs("#view-duedate")?.closest(".row, .mb-2, .form-group, tr, div");

        if (timelineSection) {
            timelineSection.classList.remove("d-none");
        }

        setText("#view-daterequested", data.dateRequested);

        if (isPermanent) {
            setText("#view-duration", "");
            setText("#view-duedate", "");

            durationRow?.classList.add("d-none");
            dueDateRow?.classList.add("d-none");
        } else {
            setText("#view-duration", data.duration);
            setText("#view-duedate", data.dueDate);

            durationRow?.classList.remove("d-none");
            dueDateRow?.classList.remove("d-none");
        }

        const status = (data.status || "").toLowerCase();
        const isReturned = status === "returned";

        const commentSection = qs("#view-comment-section");
        const commentBox = qs("#view-comment");

        const comment = (data.comment || "").trim();
        const hasComment = comment.length > 0;

        const shouldShowCommentSection = !isReturned || hasComment;

        if (commentSection) {
            commentSection.classList.toggle("d-none", !shouldShowCommentSection);
        }

        if (commentBox) {
            commentBox.value = comment;
            commentBox.readOnly = isReturned;
            commentBox.disabled = false;

            if (!shouldShowCommentSection) {
                commentBox.value = "";
            }
        }

        const returnedBtn = qs("#btn-mark-returned");
        if (returnedBtn) {
            returnedBtn.dataset.id = data.id || "";
            returnedBtn.disabled = isReturned;
        }

        hideMessage("#view-assignment-msg");

        getAssignmentViewModal()?.show();
    }

    async function handleMarkAssignmentReturned(button) {
        const id = button.dataset.id;
        if (!id) return;

        hideMessage("#return-tag-msg");
        hideMessage("#view-assignment-msg");

        const token = getAntiForgeryToken("#view-assignment-form");

        const comment = qs("#view-comment")?.value?.trim() || "";

        const body = new URLSearchParams();
        body.append("id", id);
        body.append("comment", comment);

        const result = await postForm("FacilityManager/ReturnAssignment", body, token);

        if (!result.ok) {
            showMessage("#view-assignment-msg", result.data?.message || "Failed to update assignment status.");
            return;
        }

        getAssignmentViewModal()?.hide();

        await openPage("assignments");
    }

    async function handleAssignmentSubmit(form) {
        const formData = new FormData(form);

        const response = await fetch(form.action, {
            method: "POST",
            body: formData
        });

        const data = await response.json().catch(() => null);

        if (!response.ok) {
            alert(data?.message || "Failed to create assignment.");
            return;
        }

        alert(data?.message || "Assignment created successfully.");

        await openPage("issued-overdue");
    }

    // =========================================================

    async function handleChange(event) {
        const target = event.target;

        if (target.closest("#add-key-form")) {
            hideMessage("#add-key-msg");
        }

        if (target.matches("#selectAllIndividualKeys")) {
            hideMessage("#view-key-msg");
            setAllIndividualKeyCheckboxes(target.checked);
            return;
        }

        if (target.matches(".individual-key-checkbox")) {
            hideMessage("#view-key-msg");
            syncSelectAllIndividualKeysState();
            return;
        }

        if (target.matches('input[name="CollectorType"]')) {
            syncDelegateNameState();
            return;
        }

        if (target.matches('input[name="AssignmentType"]')) {
            syncAssignmentTypeState();
        }

        if (target.matches(".graph-floor-checkbox")) {
            updateGraphFloorLabel();
            return;
        }
    }

    async function handleClick(event) {
        const target = event.target;

        const viewKeyButton = target.closest(".btn-view-key");
        if (viewKeyButton) {
            await handleViewKey(viewKeyButton);
            return;
        }

        const updateKeyButton = target.closest(".btn-update-key");
        if (updateKeyButton) {
            await openUpdateKeyModal(updateKeyButton);
            return;
        }

        const viewAssignmentButton = target.closest(".btn-view-assignment");
        if (viewAssignmentButton) {
            await handleViewAssignment(viewAssignmentButton);
            return;
        }

        if (target.closest("#btn-show-add")) {
            await openAddKeyModalForAdd();
            return;
        }

        if (target.closest("#btn-delete-key")) {
            await handleDeleteKey(target.closest("#btn-delete-key"));
            return;
        }

        if (target.closest("#btn-mark-returned")) {
            await handleMarkAssignmentReturned(target.closest("#btn-mark-returned"));
            return;
        }

        if (target.closest("#btn-close-key-view")) {
            getViewKeyModal()?.hide();
            return;
        }

        if (target.closest("#btn-close-update")) {
            getUpdateKeyModal()?.hide();
            return;
        }

        if (target.closest("#btn-close-assignment-view")) {
            getAssignmentViewModal()?.hide();
            return;
        }

        if (target.closest("#btn-cancel-add") || target.closest("#btn-close-add")) {
            getAddKeyModal()?.hide();
        }
    }

    async function handleSubmit(event) {
        const form = event.target;

        if (form.matches("#add-key-form")) {
            event.preventDefault();
            await handleAddKeySubmit(form);
            return;
        }

        if (form.matches("#update-key-form")) {
            event.preventDefault();
            await handleUpdateKeySubmit(form);
            return;
        }

        if (form.matches("#assignment-form")) {
            event.preventDefault();
            await handleAssignmentSubmit(form);
        }
    }

    async function handleInput(event) {
        const target = event.target;

        if (target.closest("#add-key-form")) {
            hideMessage("#add-key-msg");
        }

        if (target.closest("#update-key-form")) {
            hideMessage("#update-key-msg");
        }

        if (target.matches("#updateNumberOfKeysInput")) {
            await handleUpdateNumberOfKeysInput(target);
            return;
        }

        if (target.matches("#RequesterIGG")) {
            handleRequesterInput(target);
            return;
        }

        if (target.matches("#TagNumber")) {
            handleKeyTagInput(target);
            return;
        }

        if (target.matches("#Duration")) {
            calculateDueDate();
            return;
        }

        if (target.matches("#numberOfKeysInput")) {
            await handleNumberOfKeysInput(target);
        }
    }

    async function handleKeyDown(event) {
        const target = event.target;

        if (target.closest("#add-key-form") && event.key === "Enter") {
            event.preventDefault();

            if (target.matches('input[name^="TagNumbers["]')) {
                const tagInputs = qsa('#individual-key-fields input[name^="TagNumbers["]');
                const currentIndex = tagInputs.indexOf(target);

                if (currentIndex > -1 && currentIndex < tagInputs.length - 1) {
                    tagInputs[currentIndex + 1].focus();
                    tagInputs[currentIndex + 1].select?.();
                } else {
                    qs('#add-key-form button[type="submit"]')?.focus();
                }
            }

            return;
        }

        if (target.closest("#assignment-form") && event.key === "Enter") {
            event.preventDefault();
            return;
        }

        if (target.matches("#keyLookupSearch") && event.key === "Enter") {
            event.preventDefault();

            const value = (target.value || "").trim();
            await loadKeyLookupPage(value);
            return;
        }

        if (!target.matches("#numberOfKeysInput")) return;

        const blockedKeys = ["Backspace", "Delete"];

        if (
            blockedKeys.includes(event.key) &&
            target.value === "1" &&
            target.selectionStart === 0 &&
            target.selectionEnd === target.value.length
        ) {
            event.preventDefault();
        }
    }

    function handleRequesterInput(input) {
        const value = (input.value || "").trim();

        if (requesterSearchTimer) {
            clearTimeout(requesterSearchTimer);
        }

        if (value.length === 0) {
            lastRequesterSearchValue = "";
            clearRequesterFields(false);
            return;
        }

        if (value === lastRequesterSearchValue) return;

        requesterSearchTimer = setTimeout(async () => {
            lastRequesterSearchValue = value;
            await searchRequester(value);
        }, 300);
    }

    async function handleNumberOfKeysInput(input) {
        let raw = input.value;

        if (raw === "" || raw === null || raw === undefined) {
            hideMessage("#number-of-keys-msg");
            await refreshIndividualKeyInputs();
            return;
        }

        let value = parseInt(raw, 10);

        if (isNaN(value) || value < 1) {
            input.value = "1";
            hideMessage("#number-of-keys-msg");
            await refreshIndividualKeyInputs();
            return;
        }

        if (value > 10) {
            input.value = "10";
            showMessage("#number-of-keys-msg", "You can only add 10 keys at a time.");
            await refreshIndividualKeyInputs();
            return;
        }

        input.value = value.toString();

        hideMessage("#number-of-keys-msg");
        await refreshIndividualKeyInputs();
    }

    async function handleBlur(event) {
        const target = event.target;

        if (!target.matches("#numberOfKeysInput")) return;

        let value = parseInt(target.value, 10);

        if (isNaN(value) || value < 1) {
            value = 1;
        }

        if (value > 10) {
            value = 10;
            showMessage("#number-of-keys-msg", "You can only add 10 keys at a time.");
        }

        target.value = value.toString();

        await refreshIndividualKeyInputs();
    }

    function handleModalHidden(event) {
        if (event.target?.id === "addKeyModal") {
            resetAddKeyModal();
            return;
        }

        if (event.target?.id === "updateKeyModal") {
            resetUpdateKeyModal();
            return;
        }

        if (event.target?.id === "viewKeyModal") {
            resetViewKeySelections();

            const host = qs("#view-individual-keys");
            if (host) host.innerHTML = "";

            const selectAll = qs("#selectAllIndividualKeys");
            if (selectAll) {
                selectAll.checked = false;
                selectAll.indeterminate = false;
                selectAll.disabled = true;
            }

            hideMessage("#view-key-msg");
        }

        if (event.target?.id === "viewAssignmentModal") {
            const commentSection = qs("#view-comment-section");
            const commentBox = qs("#view-comment");

            const durationRow = qs("#view-duration")?.closest(".row, .mb-2, .form-group, tr, div");
            const dueDateRow = qs("#view-duedate")?.closest(".row, .mb-2, .form-group, tr, div");

            durationRow?.classList.remove("d-none");
            dueDateRow?.classList.remove("d-none");

            if (commentSection) {
                commentSection.classList.remove("d-none");
            }

            if (commentBox) {
                commentBox.value = "";
                commentBox.readOnly = false;
                commentBox.disabled = false;
            }

            hideMessage("#view-assignment-msg");
        }
    }

    function bindMenuEvents() {
        document.getElementById("btnAllKeys")?.addEventListener("click", () => openPage("keys"));
        document.getElementById("btnCreateAssignment")?.addEventListener("click", () => openPage("create-assignment"));
        document.getElementById("btnAllAssignments")?.addEventListener("click", () => openPage("assignments"));
        document.getElementById("btnIssuedandOverdueAssignments")?.addEventListener("click", () => openPage("issued-overdue"));
        document.getElementById("btnKeyLookup")?.addEventListener("click", () => openPage("key-lookup"));
    }

    function bindContainerEvents() {
        container.addEventListener("change", handleChange);
        container.addEventListener("click", handleClick);
        container.addEventListener("submit", handleSubmit);
        container.addEventListener("input", handleInput);
        container.addEventListener("keydown", handleKeyDown);
        container.addEventListener("blur", handleBlur, true);
        container.addEventListener("hidden.bs.modal", handleModalHidden);
    }

    function init() {
        bindMenuEvents();
        bindContainerEvents();

        const lastPage = sessionStorage.getItem(LAST_PAGE_KEY) || "key-lookup";
        openPage(lastPage);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();

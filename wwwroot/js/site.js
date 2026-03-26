(function () {
const facilityContentId = "facility-content";
const btnAllKeysId = "btnAllKeys";

function getFacilityContainer() {
return document.getElementById(facilityContentId);
}

function debounce(fn, delay) {
let t;
return (...args) => {
clearTimeout(t);
t = setTimeout(() => fn(...args), delay);
};
}
})();
// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Module switcher for App sidebar (Index, Payroll, Attendance, Cleaning, QrMenu)
function switchModule(module) {
    const routes = { shift: '/App', cleaning: '/Cleaning', qrmenu: '/QrMenu' };
    if (routes[module] && module !== 'shift') window.location.href = routes[module];
}

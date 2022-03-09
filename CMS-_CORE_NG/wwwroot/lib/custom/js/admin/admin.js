(function () {
    'use strict';

    $(initSidebar);

    function initSidebar() {
        $(document).ready(function () {

            $('#sidebarCollapse').on('click', function () {
                $('#sidebar').toggleClass('active');
            });

        });
    }
})();
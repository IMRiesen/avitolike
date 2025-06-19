let currentUser = null;
let cachedFavorites = [];
let notificationCount = 0;

document.addEventListener('DOMContentLoaded', async () => {
    try {
        const token = localStorage.getItem('authToken');
        if (token) {
            currentUser = await Api.getCurrentUser();
            // Добавлено: проверка и fallback для Username
            if (currentUser && !currentUser.username) {
                currentUser.username = currentUser.Email?.split('@')[0] || 'Пользователь';
                localStorage.setItem('user', JSON.stringify(currentUser));
            }
        }
    } catch (error) {
        console.error('Ошибка проверки авторизации:', error);
    }
    setupEventListeners();
    await loadAndRenderAds();
    updateNavForUser();
    updateFavoriteIndicator();
    updateNotificationCounter();
});

async function loadAndRenderAds(category = '', search = '') {
    try {
        const ads = await Api.getAds(category, search);
        renderAds(ads);
    } catch (error) {
        console.error('Ошибка загрузки объявлений:', error);
        showNotification('Ошибка при загрузке объявлений');
    }
}

async function renderAds(ads) {
    const container = document.getElementById('adsContainer');
    container.innerHTML = '';

    if (ads.length === 0) {
        container.innerHTML = `<div class="no-results"><h3>Объявления не найдены!</h3></div>`;
        return;
    }

    ads.forEach(ad => {
        const adEl = document.createElement('div');
        adEl.className = 'ad-card';

        // Добавляем класс для избранного, если объявление в избранном
        const isFavoriteClass = ad.isFavorite ? 'active' : '';

        adEl.innerHTML = `
    <div class="ad-image">
        <img src="${ad.imageUrl}" alt="${ad.title}">
    </div>
    <div class="ad-content">
        <h3 class="ad-title">${ad.title}</h3>
        <div class="ad-price">${ad.price.toLocaleString('ru-RU')} ₽</div>
        <div class="ad-meta">
            <div class="ad-location">📍 ${ad.location}</div>
            <div class="ad-date">${formatDate(ad.date)}</div>
        </div>
        <div class="ad-actions">
            <button id="favoriteBtn-${ad.id}" class="favorite-btn ${isFavoriteClass}">
                ${ad.isFavorite ? 'В избранном' : 'В избранное'}
            </button>
            ${currentUser && ad.userId === currentUser.id ?
                `<button class="edit-btn" onclick="openEditModal(${JSON.stringify(ad).replace(/"/g, '&quot;')})">✏️ Редактировать</button>` :
                ''}
        </div>
    </div>
`;

        adEl.addEventListener('click', async (e) => {
            if (!e.target.closest('.favorite-btn') && !e.target.closest('.edit-btn')) {
                const fullAd = await Api.getAdById(ad.id);
                if (fullAd) openModal(fullAd);
            }
        });

        const favoriteBtn = adEl.querySelector(`#favoriteBtn-${ad.id}`);
        favoriteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            if (!currentUser) {
                showNotification('Войдите в аккаунт, чтобы добавлять в избранное');
                openLoginModal();
                return;
            }
            toggleFavorite(ad.id);
        });

        container.appendChild(adEl);
    });
}

async function renderFavorites() {
    const container = document.getElementById('adsContainer');
    container.innerHTML = '<div class="loading">Загрузка избранного...</div>';

    try {
        // Сохраняем избранные в кеш
        cachedFavorites = await Api.getFavorites();
        const favorites = await Api.getFavorites();
        console.log("Received favorites:", favorites); // Отладка

        if (!Array.isArray(favorites)) {
            console.error('Invalid favorites format:', favorites);
            container.innerHTML = '<div class="no-results"><h3>Ошибка формата данных</h3></div>';
            return;
        }

        container.innerHTML = ''; // Очищаем контейнер

        if (favorites.length === 0) {
            container.innerHTML = '<div class="no-results"><h3>В избранном пока ничего нет</h3></div>';
            return;
        }

        favorites.forEach(ad => {
            // Проверяем наличие обязательных полей
            if (!ad.id || !ad.title) {
                console.warn('Invalid ad in favorites:', ad);
                return;
            }

            const adEl = document.createElement('div');
            adEl.className = 'ad-card';
            adEl.innerHTML = `
        <div class="ad-image">
            <img src="${ad.imageUrl || '/images/placeholder.jpg'}" alt="${ad.title}">
        </div>
        <div class="ad-content">
            <h3 class="ad-title">${ad.title}</h3>
            <div class="ad-price">${ad.price ? ad.price.toLocaleString('ru-RU') + ' ₽' : 'Цена не указана'}</div>
            <div class="ad-meta">
                <div class="ad-location">${ad.location ? '📍 ' + ad.location : ''}</div>
                <div class="ad-category">${ad.category || 'Без категории'}</div>
            </div>
            <div class="ad-actions">
                <button id="favoriteBtn-${ad.id}" class="favorite-btn active">В избранном</button>
            </div>
        </div>
        `;

            const favoriteBtn = adEl.querySelector(`#favoriteBtn-${ad.id}`);
            if (favoriteBtn) {
                favoriteBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    toggleFavorite(ad.id);
                });
            }

            adEl.addEventListener('click', async () => {
                const fullAd = await Api.getAdById(ad.id);
                if (fullAd) openModal(fullAd);
            });

            container.appendChild(adEl);
        });
    } catch (error) {
        console.error('Ошибка загрузки избранного:', error);
        container.innerHTML = `<div class="no-results"><h3>Ошибка загрузки избранного: ${error.message}</h3></div>`;
    }
}

async function filterFavoritesByCategory(category, search) {
    const container = document.getElementById('adsContainer');
    container.innerHTML = '<div class="loading">Фильтрация избранного...</div>';

    try {
        // Используем кешированные данные
        const favorites = cachedFavorites;

        // Применяем фильтры
        const filtered = favorites.filter(ad => {
            // Фильтр по категории
            if (category && category !== "" && ad.category !== category) {
                return false;
            }

            // Фильтр по поиску
            if (search) {
                const searchLower = search.toLowerCase();
                const searchTerms = [
                    ad.title?.toLowerCase() || '',
                    ad.description?.toLowerCase() || '',
                    ad.location?.toLowerCase() || '',
                    ad.price?.toString() || ''
                ];

                // Проверяем все поля на совпадение
                return searchTerms.some(term => term.includes(searchLower));
            }

            return true;
        });

        // Очищаем контейнер
        container.innerHTML = '';

        if (filtered.length === 0) {
            let message = '';
            if (search && category) {
                message = `Нет избранных в категории "${category}" по запросу "${search}"`;
            } else if (search) {
                message = `Нет избранных по запросу "${search}"`;
            } else if (category) {
                message = `Нет избранных в категории "${category}"`;
            } else {
                message = 'В избранном пока ничего нет';
            }

            container.innerHTML = `<div class="no-results"><h3>${message}</h3></div>`;
            return;
        }

        // Отображаем отфильтрованные избранные
        filtered.forEach(ad => {
            const adEl = document.createElement('div');
            adEl.className = 'ad-card';
            adEl.innerHTML = `
            <div class="ad-image">
            <img src="${ad.imageUrl || '/images/placeholder.jpg'}" alt="${ad.title}">
        </div>
        <div class="ad-content">
            <h3 class="ad-title">${ad.title}</h3>
            <div class="ad-price">${ad.price ? ad.price.toLocaleString('ru-RU') + ' ₽' : 'Цена не указана'}</div>
            <div class="ad-meta">
                <div class="ad-location">${ad.location ? '📍 ' + ad.location : ''}</div>
                <div class="ad-category">${ad.category || 'Без категории'}</div>
            </div>
            <div class="ad-actions">
                <button id="favoriteBtn-${ad.id}" class="favorite-btn active">В избранном</button>
            </div>
        </div>
        `;

            const favoriteBtn = adEl.querySelector(`#favoriteBtn-${ad.id}`);
            if (favoriteBtn) {
                favoriteBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    toggleFavorite(ad.id);
                });
            }

            adEl.addEventListener('click', async () => {
                const fullAd = await Api.getAdById(ad.id);
                if (fullAd) openModal(fullAd);
            });

            container.appendChild(adEl);
        });
    } catch (error) {
        console.error('Ошибка фильтрации избранного:', error);
        container.innerHTML = `<div class="no-results"><h3>Ошибка фильтрации: ${error.message}</h3></div>`;
    }
}

function formatDate(dateString) {
    if (!dateString) return 'Дата неизвестна';

    const date = new Date(dateString);
    if (isNaN(date)) return 'Дата неизвестна';

    const now = new Date();
    const diff = now - date;
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));

    if (days === 0) return 'Сегодня';
    if (days === 1) return 'Вчера';
    if (days < 5) return `${days} дня назад`;
    return `${days} дней назад`;
}

async function openModal(ad) {
    try {
        const modal = document.getElementById('adModal');
        if (!modal) {
            console.error('Modal element not found');
            return;
        }

        // Заполняем данные объявления
        document.getElementById('modalTitle').textContent = ad.title;
        document.getElementById('modalPrice').textContent = `${ad.price.toLocaleString('ru-RU')} ₽`;
        document.getElementById('modalDate').textContent = formatDate(ad.date);
        document.getElementById('modalViews').textContent = `${ad.views} просмотров`;
        document.getElementById('modalId').textContent = `№ ${ad.id}`;
        document.getElementById('modalDescription').textContent = ad.description;
        document.getElementById('modalLocation').textContent = `📍 ${ad.location}`;
        document.getElementById('sellerName').textContent = ad.sellerName;

        const since = new Date(ad.sellerSince);
        document.getElementById('sellerSince').textContent = `На сайте с ${since.getFullYear()}`;

        // Устанавливаем главное изображение
        const mainImg = document.getElementById('modalMainImage');
        mainImg.src = ad.imageUrl;
        mainImg.alt = ad.title;
        mainImg.style.maxWidth = '100%';
        mainImg.style.maxHeight = '100%';
        mainImg.style.objectFit = 'contain';

        // Настраиваем кнопку телефона
        const phoneBtn = document.getElementById('showPhoneBtn');
        phoneBtn.textContent = 'Показать телефон';
        phoneBtn.style.backgroundColor = '';
        phoneBtn.onclick = function () {
            this.textContent = ad.phone;
            this.style.backgroundColor = '#2ecc71';
            this.onclick = null;
        };

        // Удаляем старую кнопку избранного, если есть
        const existingFavoriteBtn = document.querySelector('.favorite-btn-container');
        if (existingFavoriteBtn) {
            existingFavoriteBtn.remove();
        }

        // Создаем контейнер для кнопок
        const favoriteBtnContainer = document.createElement('div');
        favoriteBtnContainer.className = 'favorite-btn-container';
        favoriteBtnContainer.innerHTML = `
            <button id="modalFavoriteBtn" class="favorite-btn">В избранное</button>
            ${currentUser && ad.userId === currentUser.id ?
                `<button class="edit-btn" onclick="openEditModal(${JSON.stringify(ad).replace(/"/g, '&quot;')})">✏️ Редактировать</button>` :
                ''}
        `;

        // Добавляем кнопки в модальное окно
        const modalInfo = document.querySelector('.modal-info');
        modalInfo.insertBefore(favoriteBtnContainer, modalInfo.firstChild);

        // Обработчик для кнопки избранного
        document.getElementById('modalFavoriteBtn').addEventListener('click', (e) => {
            e.stopPropagation();
            if (!currentUser) {
                showNotification('Войдите в аккаунт, чтобы добавлять в избранное');
                openLoginModal();
                return;
            }
            toggleFavorite(ad.id);
        });

        // Показываем модальное окно
        modal.style.display = 'block';
        document.body.style.overflow = 'hidden';

        // Загружаем похожие объявления
        const relevantAds = await Api.getRelevantAds(ad.id);
        renderRelevantAds(relevantAds);

        // Загружаем отзывы
        const reviews = await Api.getAdReviews(ad.id);
        renderReviews(reviews, ad.id);

        // Проверяем статус избранного
        if (currentUser) {
            checkFavoriteStatus(ad.id);
        }

    } catch (error) {
        console.error('Error in openModal:', error);
        showNotification('Ошибка при открытии объявления');
    }
}

function renderReviews(reviews, adId) {
    try {
        const modalInfo = document.querySelector('.modal-info');
        let reviewsSection = document.getElementById('reviewsSection');

        if (reviewsSection) {
            reviewsSection.remove();
        }

        reviewsSection = document.createElement('div');
        reviewsSection.id = 'reviewsSection';
        reviewsSection.className = 'reviews-section';
        reviewsSection.innerHTML = `
            <h3>Отзывы о продавце</h3>
            <div class="reviews-list"></div>
            ${currentUser && currentUser.id !== adId ? `
                <div class="add-review-form">
                    <h4>Оставить отзыв</h4>
                    <form id="reviewForm">
                        <div class="form-group">
                            <label>Оценка</label>
                            <div class="rating-stars">
                                ${[1, 2, 3, 4, 5].map(i => `
                                    <span class="star" data-rating="${i}">☆</span>
                                `).join('')}
                            </div>
                            <input type="hidden" id="reviewRating" name="rating" value="5">
                        </div>
                        <div class="form-group">
                            <textarea id="reviewComment" name="comment" placeholder="Ваш отзыв" required></textarea>
                        </div>
                        <button type="submit" class="submit-btn">Отправить отзыв</button>
                    </form>
                </div>
            ` : currentUser ? '<p>Вы не можете оставить отзыв на свое объявление</p>' : '<p>Войдите, чтобы оставить отзыв</p>'}
        `;

        const relevantSection = document.getElementById('relevantAdsSection');
        if (relevantSection) {
            relevantSection.insertAdjacentElement('afterend', reviewsSection);
        } else {
            const locationElement = document.getElementById('modalLocation');
            if (locationElement) {
                locationElement.insertAdjacentElement('afterend', reviewsSection);
            }
        }

        // Обработчик звезд рейтинга
        reviewsSection.querySelectorAll('.star').forEach(star => {
            star.addEventListener('click', function () {
                const rating = parseInt(this.dataset.rating);
                document.getElementById('reviewRating').value = rating;

                // Обновляем отображение звезд
                const stars = reviewsSection.querySelectorAll('.star');
                stars.forEach((s, i) => {
                    s.textContent = i < rating ? '★' : '☆';
                    s.style.color = i < rating ? '#ffc107' : '#ccc';
                });
            });
        });

        // Обработчик формы отзыва
        if (currentUser) {
            const reviewForm = reviewsSection.querySelector('#reviewForm');
            if (reviewForm) {
                reviewForm.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    const rating = parseInt(document.getElementById('reviewRating').value);
                    const comment = document.getElementById('reviewComment').value.trim();

                    if (!comment) {
                        showNotification('Пожалуйста, напишите отзыв');
                        return;
                    }

                    try {
                        const newReview = await Api.addReview(adId, {
                            Rating: rating,
                            Comment: comment
                        });

                        // Добавляем новый отзыв в список
                        const reviewsList = reviewsSection.querySelector('.reviews-list');
                        if (reviewsList.innerHTML === '<p>Пока нет отзывов</p>') {
                            reviewsList.innerHTML = '';
                        }
                        reviewsList.appendChild(createReviewElement(newReview));

                        // Очищаем форму
                        reviewForm.reset();
                        document.getElementById('reviewRating').value = '5';
                        reviewsSection.querySelectorAll('.star').forEach(star => {
                            star.textContent = '☆';
                            star.style.color = '#ccc';
                        });

                        showNotification('Отзыв успешно добавлен');
                    } catch (error) {
                        console.error('Review submission error:', error);
                        showNotification('Ошибка при добавлении отзыва: ' + (error.message || 'Попробуйте позже'));
                    }
                });
            }
        }

        // Добавляем существующие отзывы
        const reviewsList = reviewsSection.querySelector('.reviews-list');
        if (reviews && reviews.length > 0) {
            reviews.forEach(review => {
                reviewsList.appendChild(createReviewElement(review));
            });
        } else {
            reviewsList.innerHTML = '<p>Пока нет отзывов</p>';
        }
    } catch (error) {
        console.error('Error rendering reviews:', error);
    }
}

function createReviewElement(review) {
    const reviewEl = document.createElement('div');
    reviewEl.className = 'review-item';

    // Создаем звезды рейтинга
    const stars = [];
    for (let i = 1; i <= 5; i++) {
        stars.push(i <= review.rating ? '★' : '☆');
    }

    reviewEl.innerHTML = `
        <div class="review-header">
            <div class="review-author">${review.authorName}</div>
            <div class="review-rating" style="color: #ffc107;">${stars.join('')}</div>
            <div class="review-date">${formatDate(review.date)}</div>
        </div>
        <div class="review-comment">${review.comment}</div>
    `;

    return reviewEl;
}

function renderRelevantAds(ads) {
    const modalInfo = document.querySelector('.modal-info');
    let relevantSection = document.getElementById('relevantAdsSection');

    if (relevantSection) {
        relevantSection.remove();
    }

    if (ads.length === 0) return;

    relevantSection = document.createElement('div');
    relevantSection.id = 'relevantAdsSection';
    relevantSection.className = 'relevant-ads-section';
    relevantSection.innerHTML = `
        <h3>Похожие объявления</h3>
        <div class="relevant-ads-grid"></div>
    `;

    const grid = relevantSection.querySelector('.relevant-ads-grid');

    ads.forEach(ad => {
        const adEl = document.createElement('div');
        adEl.className = 'relevant-ad-card';
        adEl.innerHTML = `
            <div class="relevant-ad-image">
                <img src="${ad.imageUrl}" alt="${ad.title}">
            </div>
            <div class="relevant-ad-content">
                <div class="relevant-ad-title">${ad.title}</div>
                <div class="relevant-ad-price">${ad.price.toLocaleString('ru-RU')} ₽</div>
            </div>
        `;

        // ИСПРАВЛЕНИЕ: Загружаем полные данные объявления при клике
        adEl.addEventListener('click', async () => {
            closeModal();
            // Даем время на закрытие модального окна
            setTimeout(async () => {
                const fullAd = await Api.getAdById(ad.id);
                if (fullAd) {
                    openModal(fullAd);
                }
            }, 100);
        });

        grid.appendChild(adEl);
    });

    const locationElement = document.getElementById('modalLocation');
    if (locationElement) {
        locationElement.insertAdjacentElement('afterend', relevantSection);
    }
}

function closeModal() {
    const modal = document.getElementById('adModal');
    modal.style.display = 'none';
    document.body.style.overflow = 'auto';
}

function openAddModal() {
    if (!currentUser) {
        showNotification('Войдите в аккаунт, чтобы добавлять объявления');
        openLoginModal();
        return;
    }
    document.getElementById('addAdModal').style.display = 'block';
    document.body.style.overflow = 'hidden';
}

function closeAddModal() {
    document.getElementById('addAdModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

function openEditModal(ad) {
    const modal = document.getElementById('editAdModal');

    document.getElementById('editAdTitle').value = ad.title;
    document.getElementById('editAdDescription').value = ad.description;

    // Принудительно вызываем событие input, чтобы textarea расширилась
    const editDescriptionTextarea = document.getElementById('editAdDescription');
    editDescriptionTextarea.dispatchEvent(new Event('input')); // Это заставит обработчик сработать

    document.getElementById('editAdPrice').value = ad.price;

    const categorySelect = document.getElementById('editAdCategory');
    categorySelect.value = ad.categoryId || 4;

    document.getElementById('editAdLocation').value = ad.location;

    const preview = document.getElementById('editPreviewImage');
    preview.src = ad.imageUrl;
    preview.style.display = 'block';

    modal.dataset.adId = ad.id;
    modal.style.display = 'block';
    document.body.style.overflow = 'hidden';
}

function closeEditModal() {
    document.getElementById('editAdModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

function openLoginModal() {
    document.getElementById('loginModal').style.display = 'block';
    document.body.style.overflow = 'hidden';
}

function closeLoginModal() {
    document.getElementById('loginModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

function openRegisterModal() {
    closeLoginModal();
    document.getElementById('registerModal').style.display = 'block';
    document.body.style.overflow = 'hidden';
    applyPhoneMask(document.getElementById('registerPhone'));
}

function closeRegisterModal() {
    document.getElementById('registerModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

async function toggleFavorite(adId) {
    if (!currentUser) return;

    const result = await Api.toggleFavorite(adId);
    updateFavoriteButton(adId, result.isFavorite);
    updateFavoriteIndicator();
    updateNotificationCounter();

    // Обновляем отображение избранного, если мы на соответствующей вкладке
    if (document.querySelector('.nav-link.active').id === 'favoritesLink') {
        await renderFavorites();
    }

    if (result.isFavorite) {
        showNotification('Объявление добавлено в избранное');
    } else {
        showNotification('Объявление удалено из избранного');
    }
}

function updateFavoriteButton(adId, isFavorite) {
    const btn = document.getElementById(`favoriteBtn-${adId}`);
    if (btn) {
        btn.innerHTML = isFavorite ? 'В избранном' : 'В избранное';
        btn.className = isFavorite ? 'favorite-btn active' : 'favorite-btn';
    }

    // Обновляем кнопку в модальном окне, если она есть
    const modalBtn = document.getElementById('modalFavoriteBtn');
    if (modalBtn && modalBtn.closest('.modal-content')) {
        modalBtn.innerHTML = isFavorite ? 'В избранном' : 'В избранное';
        modalBtn.className = isFavorite ? 'favorite-btn active' : 'favorite-btn';
    }
}

async function checkFavoriteStatus(adId) {
    if (!currentUser) return;

    try {
        const result = await Api.isFavorite(adId);
        updateFavoriteButton(adId, result.isFavorite);
    } catch (error) {
        console.error('Ошибка проверки избранного:', error);
    }
}

function showNotification(message) {
    const notification = document.createElement('div');
    notification.className = 'notification';
    notification.textContent = message;
    document.body.appendChild(notification);

    setTimeout(() => {
        notification.classList.add('fade-out');
        setTimeout(() => notification.remove(), 500);
    }, 3000);
}

async function submitEditForm(e) {
    e.preventDefault();

    const form = document.getElementById('editAdForm');
    const adId = document.getElementById('editAdModal').dataset.adId;

    let imageUrl = document.getElementById('editPreviewImage').src;
    const imageFile = document.getElementById('editAdImage').files[0];

    if (imageFile) {
        const reader = new FileReader();
        reader.onloadend = function () {
            imageUrl = reader.result;
            updateAd(adId, form, imageUrl);
        };
        reader.readAsDataURL(imageFile);
    } else {
        await updateAd(adId, form, imageUrl);
    }
}

async function updateAd(adId, form, imageUrl) {
    const adData = {
        Id: parseInt(adId),
        Title: form.title.value,
        Description: form.description.value,
        Price: parseFloat(form.price.value),
        CategoryId: parseInt(form.category.value),
        Location: form.location.value,
        ImageUrl: imageUrl
    };

    try {
        // Вызываем API для обновления
        await Api.updateAd(adId, adData);

        showNotification('Объявление успешно обновлено');
        closeEditModal();

        // Обновляем данные на странице
        const fullAd = await Api.getAdById(adId);
        if (fullAd) openModal(fullAd);
        await loadAndRenderAds();
    } catch (error) {
        console.error('Ошибка обновления:', error);
        showNotification('Ошибка при обновлении: ' + error.message);
    }
}

function setupEventListeners() {
    // Поиск объявлений
    document.getElementById('searchButton').addEventListener('click', async () => {
        await handleSearch();
    });

    document.getElementById('searchInput').addEventListener('keypress', async (e) => {
        if (e.key === 'Enter') {
            await handleSearch();
        }
    });

    async function handleSearch() {
        const activeNavLink = document.querySelector('.nav-link.active');
        const isFavoritesActive = activeNavLink && activeNavLink.id === 'favoritesLink';

        const searchText = document.getElementById('searchInput').value;
        const categoryBtn = document.querySelector('.category-btn.active');
        const category = categoryBtn ? categoryBtn.dataset.category : '';

        if (isFavoritesActive) {
            // Для вкладки "Избранное" применяем локальную фильтрацию
            await filterFavoritesByCategory(category, searchText);
        } else {
            // Для других вкладок выполняем обычный поиск
            await loadAndRenderAds(category, searchText);
        }
    }

    // Фильтрация по категориям
    document.querySelectorAll('.category-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const activeNavLink = document.querySelector('.nav-link.active');
            const isFavoritesActive = activeNavLink && activeNavLink.id === 'favoritesLink';

            document.querySelectorAll('.category-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            const searchText = document.getElementById('searchInput').value;
            const category = btn.dataset.category;

            if (isFavoritesActive) {
                await filterFavoritesByCategory(category, searchText);
            } else {
                await loadAndRenderAds(category, searchText);
            }
        });
    });

    // Модальные окна
    document.querySelectorAll('.close').forEach(btn => {
        btn.addEventListener('click', function () {
            const modal = this.closest('.modal');
            if (modal) modal.style.display = 'none';
            document.body.style.overflow = 'auto';
        });
    });

    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                modal.style.display = 'none';
                document.body.style.overflow = 'auto';
            }
        });
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            const openModal = document.querySelector('.modal[style="display: block;"]');
            if (openModal) {
                openModal.style.display = 'none';
                document.body.style.overflow = 'auto';
            }
        }
    });

    // Навигационное меню
    document.querySelectorAll('.nav-link').forEach(link => {
        link.addEventListener('click', async (e) => {
            e.preventDefault();

            // Удаляем активный класс у всех ссылок
            document.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));

            // Добавляем активный класс только текущей ссылке
            link.classList.add('active');

            // Проверяем ID ссылки для точной идентификации
            if (link.id === 'favoritesLink') {
                if (!currentUser) {
                    showNotification('Войдите в аккаунт, чтобы просматривать избранное');
                    openLoginModal();
                    return;
                }
                await renderFavorites();
            }
            else if (link.id === 'addLink') {
                openAddModal();
            }
            else if (link.id === 'userCabinetLink') {
                if (!currentUser) {
                    showNotification('Войдите в аккаунт для просмотра личного кабинета');
                    openLoginModal();
                    return;
                }
                openUserCabinetModal();
            }
            else if (link.id === 'authLink') {
                if (currentUser) {
                    logout();
                } else {
                    openLoginModal();
                }
            }
            else {
                // Для всех остальных ссылок (включая "Главная")
                document.querySelector('.main').style.display = 'block';
                await loadAndRenderAds();
            }
        });
    });

    // Автоматическое изменение высоты textarea
    const descriptionTextarea = document.getElementById('adDescription');
    descriptionTextarea.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = this.scrollHeight + 'px';
    });

    const editDescriptionTextarea = document.getElementById('editAdDescription');
    editDescriptionTextarea.addEventListener('input', function () {
        this.style.height = 'auto';
        this.style.height = this.scrollHeight + 'px';
    });

    // Превью изображений
    document.getElementById('adImage').addEventListener('change', function (e) {
        const file = e.target.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = function (event) {
                const preview = document.getElementById('previewImage');
                preview.src = event.target.result;
                preview.style.display = 'block';
            }
            reader.readAsDataURL(file);
        }
    });

    document.getElementById('editAdImage').addEventListener('change', function (e) {
        const file = e.target.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = function (event) {
                const preview = document.getElementById('editPreviewImage');
                preview.src = event.target.result;
                preview.style.display = 'block';
            }
            reader.readAsDataURL(file);
        }
    });

    // Форма добавления объявления
    document.getElementById('addAdForm').addEventListener('submit', async (e) => {
        e.preventDefault();

        let imageUrl = '/images/placeholder.jpg';
        const imageFile = document.getElementById('adImage').files[0];

        if (imageFile) {
            const reader = new FileReader();
            reader.onloadend = async function () {
                imageUrl = reader.result;
                const ad = buildAdData(imageUrl);
                await submitAd(ad);
            };
            reader.readAsDataURL(imageFile);
        } else {
            const ad = buildAdData(imageUrl);
            await submitAd(ad);
        }
    });

    // Форма редактирования объявления
    document.getElementById('editAdForm').addEventListener('submit', submitEditForm);

    // Форма входа
    document.getElementById('loginForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = document.getElementById('loginEmail').value.trim();
        const password = document.getElementById('loginPassword').value;

        try {
            const response = await Api.login({ email, password });
            if (response) {
                currentUser = response;
                closeLoginModal();
                showNotification(`Добро пожаловать, ${currentUser.username}!`);
                updateNavForUser();
                await loadAndRenderAds();
                updateFavoriteIndicator();
                updateNotificationCounter();
            }
        } catch (error) {
            showNotification('Ошибка входа: ' + error.message);
        }
    });

    document.getElementById('registerForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        const username = document.getElementById('registerUsername').value.trim();
        const email = document.getElementById('registerEmail').value.trim();
        const password = document.getElementById('registerPassword').value;
        const phone = document.getElementById('registerPhone').value.trim();

        try {
            const response = await Api.register({
                username,
                email,
                password,
                phone: phone || undefined
            });

            // Успешная регистрация, даже если ответ не содержит всех ожидаемых полей
            if (response && response.id) {
                currentUser = {
                    id: response.id,
                    username: response.username || username,
                    email: response.email || email,
                    avatarUrl: response.avatarUrl || '',
                    roles: response.roles || ['user'],
                    createdAt: response.createdAt || new Date().toISOString()
                };

                localStorage.setItem('user', JSON.stringify(currentUser));
                localStorage.setItem('authToken', response.token || '');

                closeRegisterModal();
                showNotification(`Регистрация успешна, ${currentUser.username}!`);
                updateNavForUser();
                await loadAndRenderAds();
                updateFavoriteIndicator();
                updateNotificationCounter();
            } else {
                showNotification('Регистрация прошла успешно, выполните вход');
                openLoginModal();
            }
        } catch (error) {
            console.error('Registration error:', error);
            showNotification('Ошибка регистрации: ' + (error.message || 'Попробуйте позже'));
        }
    });
    document.getElementById('notificationsLink').addEventListener('click', openNotificationsModal);
    document.getElementById('markAllAsReadBtn').addEventListener('click', markAllNotificationsAsRead);
}

// Функции для работы с уведомлениями
async function updateNotificationCounter() {
    const counter = document.getElementById('notificationCounter');
    if (!currentUser) {
        counter.style.display = 'none';
        return;
    }

    try {
        const response = await fetch('/api/notifications/unread-count', {
            headers: Api.getAuthHeader()
        });

        if (response.ok) {
            const data = await response.json();
            notificationCount = data.count;

            if (notificationCount > 0) {
                counter.textContent = notificationCount;
                counter.style.display = 'inline-flex';
            } else {
                counter.style.display = 'none';
            }
        }
    } catch (error) {
        console.error('Ошибка получения счетчика уведомлений:', error);
    }
}

async function openNotificationsModal() {
    if (!currentUser) {
        showNotification('Войдите в аккаунт для просмотра уведомлений');
        openLoginModal();
        return;
    }

    document.getElementById('notificationsModal').style.display = 'block';
    document.body.style.overflow = 'hidden';
    loadNotifications();
    updateNotificationCounter();
}

function closeNotificationsModal() {
    document.getElementById('notificationsModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

async function loadNotifications() {
    try {
        const response = await fetch('/api/notifications', {
            headers: Api.getAuthHeader()
        });

        if (!response.ok) throw new Error('Ошибка загрузки уведомлений');

        const notifications = await response.json();
        renderNotifications(notifications);
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка загрузки уведомлений');
    }
}

function renderNotifications(notifications) {
    const container = document.getElementById('notificationsList');
    container.innerHTML = '';

    if (notifications.length === 0) {
        container.innerHTML = '<p>У вас пока нет уведомлений</p>';
        return;
    }

    notifications.forEach(notif => {
        const notifEl = document.createElement('div');
        notifEl.className = `notification-item ${notif.isRead ? '' : 'unread'}`;
        notifEl.dataset.id = notif.id;
        notifEl.innerHTML = `
            <div class="notification-icon-item">${notif.icon || '🔔'}</div>
            <div class="notification-content">
                <div class="notification-title">${notif.title}</div>
                <div class="notification-message">${notif.message}</div>
                <div class="notification-date">${formatDate(notif.createdAt)}</div>
            </div>
        `;

        notifEl.addEventListener('click', () => {
            if (!notif.isRead) {
                markNotificationAsRead(notif.id);
                notifEl.classList.remove('unread');
                notificationCount--;
                updateNotificationCounter();
            }
            handleNotificationClick(notif);
        });

        container.appendChild(notifEl);
    });
}

async function markNotificationAsRead(notificationId) {
    try {
        await fetch(`/api/notifications/${notificationId}/read`, {
            method: 'POST',
            headers: Api.getAuthHeader()
        });
        updateNotificationCounter();
    } catch (error) {
        console.error('Ошибка пометки уведомления:', error);
    }
}

async function markAllNotificationsAsRead() {
    try {
        await fetch('/api/notifications/mark-all-read', {
            method: 'POST',
            headers: Api.getAuthHeader()
        });
        notificationCount = 0;
        updateNotificationCounter();
        loadNotifications();
    } catch (error) {
        console.error('Ошибка пометки всех уведомлений:', error);
    }
}

function handleNotificationClick(notification) {
    closeNotificationsModal();

    switch (notification.type) {
        case 'favorite':
            // Показ объявления
            openAdModal(notification.relatedId);
            break;
        case 'price_change':
            // Показ объявления
            openAdModal(notification.relatedId);
            break;
        case 'new_review':
            // Показ профиля пользователя
            openUserProfile(notification.authorId);
            break;
        default:
            // Ничего не делать
            break;
    }
}

function buildAdData(imageUrl) {
    const form = document.getElementById('addAdForm');

    const categoryMap = {
        "Техника": 1,
        "Недвижимость": 2,
        "Транспорт": 3,
        "Другое": 4
    };

    const categoryId = categoryMap[form.category.value] || 4;

    return {
        title: form.title.value,
        description: form.description.value,
        price: parseFloat(form.price.value),
        categoryId: categoryId,
        location: form.location.value,
        imageUrl: imageUrl
    };
}


async function submitAd(ad) {
    const success = await Api.createAd(ad);
    if (success) {
        showNotification('Объявление добавлено!');
        document.getElementById('addAdForm').reset();
        document.getElementById('previewImage').style.display = 'none';
        closeAddModal();
        await loadAndRenderAds();
    } else {
        showNotification('Ошибка при добавлении.');
    }
}

function applyPhoneMask(input) {
    input.addEventListener('input', function () {
        let x = input.value.replace(/\D/g, '').substring(1);
        let formatted = '+7';
        if (x.length > 0) formatted += ' (' + x.substring(0, 3);
        if (x.length >= 3) formatted += ') ' + x.substring(3, 6);
        if (x.length >= 6) formatted += '-' + x.substring(6, 8);
        if (x.length >= 8) formatted += '-' + x.substring(8, 10);
        input.value = formatted;
    });
}

async function updateFavoriteIndicator() {
    const counter = document.getElementById('favoriteCounter');
    if (!currentUser) {
        counter.style.display = 'none';
        return;
    }

    const favorites = await Api.getFavorites(currentUser.id);
    counter.textContent = favorites.length;
    counter.style.display = favorites.length > 0 ? 'inline-flex' : 'none';
}

function updateNavForUser() {
    const authLink = document.getElementById('authLink');
    const userCabinetLink = document.getElementById('userCabinetLink');

    if (currentUser) {
        authLink.textContent = 'Выйти';
        authLink.onclick = (e) => {
            e.preventDefault();
            logout();
        };

        userCabinetLink.style.display = 'inline-block';
        userCabinetLink.onclick = (e) => {
            e.preventDefault();
            openUserCabinetModal();
        };
    } else {
        authLink.textContent = 'Войти';
        authLink.onclick = (e) => {
            e.preventDefault();
            openLoginModal();
        };

        userCabinetLink.style.display = 'none';
    }
}
function logout() {
    currentUser = null;
    showNotification('Вы вышли из аккаунта');
    updateNavForUser();
    loadAndRenderAds();
    updateFavoriteIndicator();
    updateNotificationCounter();
}
async function loadUserAds() {
    if (!currentUser) {
        showNotification('Войдите в аккаунт для просмотра личного кабинета');
        openLoginModal();
        return;
    }

    const userAds = await Api.getUserAds(currentUser.id);
    renderAds(userAds);

    if (userAds.length === 0) {
        showNotification('У вас пока нет объявлений');
    }
}
function openUserCabinetModal() {
    if (!currentUser) {
        showNotification('Войдите в аккаунт для просмотра личного кабинета');
        openLoginModal();
        return;
    }

    document.getElementById('cabinetUserName').textContent = currentUser.username;

    if (currentUser.createdAt) {
        const since = new Date(currentUser.createdAt);
        document.getElementById('cabinetUserSince').textContent = `На сайте с ${since.getFullYear()}`;
    } else {
        document.getElementById('cabinetUserSince').textContent = 'Дата регистрации неизвестна';
    }

    // Загрузка статистики
    loadUserStats();
    loadUserAdsForCabinet();

    document.getElementById('userCabinetModal').style.display = 'block';
    document.body.style.overflow = 'hidden';
}

function closeUserCabinetModal() {
    document.getElementById('userCabinetModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

async function loadUserStats() {
    // Добавлена проверка
    if (!currentUser || !currentUser.id) {
        console.warn('Пользователь не определен');
        return;
    }

    const userAds = await Api.getUserAds(currentUser.id);
    const favorites = await Api.getFavorites(currentUser.id);

    document.getElementById('totalAdsCount').textContent = userAds.length;
    document.getElementById('totalViewsCount').textContent = userAds.reduce((sum, ad) => sum + ad.views, 0);
    document.getElementById('favoritesCount').textContent = favorites.length;
}

async function loadUserAdsForCabinet() {
    // Добавлена проверка
    if (!currentUser || !currentUser.id) {
        console.warn('Пользователь не определен');
        return;
    }

    const userAds = await Api.getUserAds(currentUser.id);
    const container = document.getElementById('userAdsContainer');
    container.innerHTML = '';

    if (userAds.length === 0) {
        container.innerHTML = '<p>У вас пока нет объявлений</p>';
        return;
    }

    userAds.forEach(ad => {
        const adEl = document.createElement('div');
        adEl.className = 'user-ad-card';
        adEl.innerHTML = `
            <div class="user-ad-image">
                <img src="${ad.imageUrl}" alt="${ad.title}">
            </div>
            <div class="user-ad-content">
                <div class="user-ad-title">${ad.title}</div>
                <div class="user-ad-price">${ad.price.toLocaleString('ru-RU')} ₽</div>
                <div class="user-ad-actions">
                    <button class="user-ad-btn edit-btn" onclick="openEditModal(${JSON.stringify(ad).replace(/"/g, '&quot;')}); closeUserCabinetModal()">✏️</button>
                    <button class="user-ad-btn delete-btn-outline" onclick="deleteAd(${ad.id})">🗑️</button>
                </div>
            </div>
        `;

        container.appendChild(adEl);
    });
}

window.deleteAd = async function (adId) {
    if (!confirm('Вы уверены, что хотите полностью удалить это объявление? Это действие нельзя отменить.')) return;

    try {
        await Api.deleteAd(adId);
        showNotification('Объявление полностью удалено');

        // Обновляем все необходимые разделы
        await Promise.all([
            loadUserAdsForCabinet(),
            loadUserStats(),
            loadAndRenderAds()
        ]);

        // Закрываем модальное окно, если оно открыто
        const modal = document.getElementById('adModal');
        if (modal.style.display === 'block') {
            closeModal();
        }

        // Если находимся в избранном, обновляем его
        if (document.querySelector('.nav-link.active').id === 'favoritesLink') {
            await renderFavorites();
        }
    } catch (error) {
        console.error('Ошибка удаления:', error);
        showNotification('Ошибка при удалении объявления: ' + error.message);
    }
}
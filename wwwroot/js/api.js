class Api {
    static baseUrl = 'http://myavitoapp.zapto.org:5000/api';
    static adsUrl = 'http://myavitoapp.zapto.org:5000/api/ads';

    // Методы для работы с объявлениями
    static async getAds(category = '', search = '') {
        try {
            const params = new URLSearchParams();
            if (category) params.append('category', category);
            if (search) params.append('search', search);

            // Добавляем параметр для запроса статуса избранного
            if (currentUser) {
                params.append('includeFavoriteStatus', 'true');
            }

            const url = `${this.adsUrl}?${params.toString()}`;

            const response = await fetch(url, {
                headers: this.getAuthHeader()
            });

            if (!response.ok) throw new Error('Ошибка загрузки');
            const data = await response.json();
            return data.data || [];
        } catch (error) {
            console.error('Ошибка:', error);
            return [];
        }
    }

    static async getAdById(id) {
        try {
            const response = await fetch(`${this.adsUrl}/${id}`, {
                headers: this.getAuthHeader()
            });
            if (!response.ok) throw new Error('Ошибка загрузки');
            return await response.json();
        } catch (error) {
            console.error('Ошибка:', error);
            return null;
        }
    }

    static async updateAd(id, adData) {
        try {
            const response = await fetch(`${this.adsUrl}/${id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    ...this.getAuthHeader()
                },
                body: JSON.stringify(adData)
            });

            if (response.status === 204) { // Обрабатываем No Content
                return { success: true };
            }

            if (!response.ok) {
                let errorText = await response.text();
                try {
                    const errorJson = JSON.parse(errorText);
                    errorText = errorJson.error || errorJson.message || errorText;
                } catch {
                    // Оставляем текст как есть
                }
                throw new Error(errorText || 'Ошибка обновления объявления');
            }

            return await response.json();
        } catch (error) {
            console.error('Ошибка обновления:', error);
            throw error;
        }
    }

    static async getRelevantAds(adId) {
        try {
            const response = await fetch(`${this.baseUrl}/ads/relevant?adId=${adId}`, {
                headers: this.getAuthHeader()
            });
            return await response.json();
        } catch (error) {
            console.error('Error loading relevant ads:', error);
            return [];
        }
    }

    static async createAd(ad) {
        try {
            const response = await fetch(this.adsUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...this.getAuthHeader()
                },
                body: JSON.stringify(ad)
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Ошибка создания объявления');
            }

            return await response.json();
        } catch (error) {
            console.error('Ошибка добавления:', error);
            throw error;
        }
    }

    static async deleteAd(id) {
        try {
            const response = await fetch(`${this.adsUrl}/${id}`, {
                method: 'DELETE',
                headers: this.getAuthHeader()
            });

            if (response.status === 204) {
                return true;
            }

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Ошибка удаления объявления');
            }

            return true;
        } catch (error) {
            console.error('Ошибка удаления:', error);
            throw error;
        }
    }

    // Методы аутентификации
    static async register(userData) {
        try {
            const response = await fetch(`${this.baseUrl}/auth/register`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(userData)
            });

            // Добавим подробное логирование для отладки
            console.log('Register response status:', response.status);
            const responseBody = await response.text();
            console.log('Register response body:', responseBody);

            if (!response.ok) {
                try {
                    const errorData = JSON.parse(responseBody);
                    throw new Error(errorData.message || 'Ошибка при регистрации');
                } catch (e) {
                    throw new Error(responseBody || 'Ошибка при регистрации');
                }
            }

            const data = JSON.parse(responseBody);
            console.log('Parsed register data:', data);

            // Нормализуем структуру ответа
            const token = data.token || data.Token;
            const createdAt = data.createdAt || data.CreatedAt || new Date().toISOString();
            const user = data.user || {
                id: data.id,
                username: data.username,
                email: data.email,
                avatarUrl: data.avatarUrl,
                roles: data.roles,
                createdAt: createdAt
            };

            if (token) {
                localStorage.setItem('authToken', token);
                localStorage.setItem('user', JSON.stringify({
                    id: user.id,
                    username: user.username,
                    email: user.email,
                    avatarUrl: user.avatarUrl,
                    roles: user.roles,
                    createdAt: createdAt
                }));
            }

            return {
                id: user.id,
                username: user.username,
                email: user.email,
                avatarUrl: user.avatarUrl,
                roles: user.roles,
                createdAt: createdAt,
                token: token
            };
        } catch (error) {
            console.error('Ошибка регистрации:', error);
            throw new Error(error.message || 'Ошибка при регистрации');
        }
    }


    static async login(credentials) {
        try {
            const response = await fetch(`${this.baseUrl}/auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(credentials)
            });

            console.log('Login response status:', response.status);
            const responseBody = await response.text();
            console.log('Login response body:', responseBody);

            if (!response.ok) {
                try {
                    const errorData = JSON.parse(responseBody);
                    throw new Error(errorData.message || 'Ошибка входа');
                } catch (e) {
                    throw new Error(responseBody || 'Ошибка входа');
                }
            }

            const data = JSON.parse(responseBody);
            console.log('Parsed login data:', data);

            // Нормализуем структуру ответа
            const token = data.token || data.Token;
            const createdAt = data.createdAt || data.CreatedAt;
            const user = data.user || {
                id: data.id,
                username: data.username,
                email: data.email,
                avatarUrl: data.avatarUrl,
                roles: data.roles,
                createdAt: createdAt
            };

            if (token) {
                localStorage.setItem('authToken', token);
                localStorage.setItem('user', JSON.stringify({
                    id: user.id,
                    username: user.username,
                    email: user.email,
                    avatarUrl: user.avatarUrl,
                    roles: user.roles,
                    createdAt: createdAt
                }));
            }

            return {
                id: user.id,
                username: user.username,
                email: user.email,
                avatarUrl: user.avatarUrl,
                roles: user.roles,
                createdAt: createdAt,
                token: token
            };
        } catch (error) {
            console.error('Ошибка входа:', error);
            throw error;
        }
    }


    static logout() {
        localStorage.removeItem('authToken');
        localStorage.removeItem('user');
    }

    static getAuthHeader() {
        const token = localStorage.getItem('authToken');
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    }

    // Методы для избранного
    static async toggleFavorite(adId) {
        try {
            const response = await fetch(`${this.baseUrl}/ads/favorites/check?adId=${adId}`, {
                headers: this.getAuthHeader()
            });

            const { isFavorite } = await response.json();

            if (isFavorite) {
                return this.removeFromFavorites(adId);
            } else {
                return this.addToFavorites(adId);
            }
        } catch (error) {
            console.error('Ошибка переключения избранного:', error);
            throw error;
        }
    }

    static async addToFavorites(adId) {
        try {
            const response = await fetch(`${this.baseUrl}/ads/favorites`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...this.getAuthHeader()
                },
                body: JSON.stringify({ adId })
            });

            if (!response.ok) throw new Error('Ошибка добавления');
            return { isFavorite: true };
        } catch (error) {
            console.error('Ошибка добавления в избранное:', error);
            throw error;
        }
    }

    static async removeFromFavorites(adId) {
        try {
            const response = await fetch(`${this.baseUrl}/ads/favorites/${adId}`, {
                method: 'DELETE',
                headers: this.getAuthHeader()
            });

            // Исправленная проверка ответа
            if (response.ok) {
                return { isFavorite: false };
            } else {
                const error = await response.text();
                throw new Error(error || 'Ошибка удаления');
            }
        } catch (error) {
            console.error('Ошибка удаления из избранного:', error);
            throw error;
        }
    }

    static async getFavorites() {
        try {
            const response = await fetch(`${this.baseUrl}/ads/favorites`, {
                headers: this.getAuthHeader()
            });

            if (!response.ok) {
                const errorData = await response.json();
                const errorMsg = errorData.error || "Ошибка загрузки избранного";
                const details = errorData.details ? ` (${errorData.details})` : "";
                throw new Error(errorMsg + details);
            }

            return await response.json();
        } catch (error) {
            console.error('Ошибка загрузки избранного:', error);
            // Показываем пользователю понятное сообщение
            showNotification(`Ошибка загрузки избранного: ${error.message}`);
            return [];
        }
    }

    static async isFavorite(adId) {
        try {
            const response = await fetch(`${this.baseUrl}/ads/favorites/check?adId=${adId}`, {
                headers: this.getAuthHeader()
            });
            return await response.json();
        } catch (error) {
            console.error('Ошибка проверки избранного:', error);
            return { isFavorite: false };
        }
    }

    // Методы для пользователя
    static async getCurrentUser() {
        const userJson = localStorage.getItem('user');
        if (userJson) {
            try {
                const user = JSON.parse(userJson);
                // Добавляем fallback для обязательных полей
                if (!user.createdAt) {
                    user.createdAt = new Date().toISOString();
                    localStorage.setItem('user', JSON.stringify(user));
                }
                return user;
            } catch (e) {
                console.error('Error parsing user from localStorage:', e);
            }
        }

        try {
            const response = await fetch(`${this.baseUrl}/auth/current`, {
                headers: this.getAuthHeader()
            });

            if (!response.ok) {
                if (response.status === 401) {
                    this.logout();
                    return null;
                }
                throw new Error('Ошибка загрузки пользователя');
            }

            const data = await response.json();
            console.log('Current user response:', data);

            // Нормализуем структуру ответа
            const user = data.user || {
                id: data.id,
                username: data.username,
                email: data.email,
                avatarUrl: data.avatarUrl,
                roles: data.roles,
                createdAt: data.createdAt || data.CreatedAt || new Date().toISOString()
            };

            localStorage.setItem('user', JSON.stringify(user));
            return user;
        } catch (error) {
            console.error('Ошибка:', error);
            return null;
        }
    }

    static async getUserAds(userId) {
        try {
            if (!userId) throw new Error('Invalid user ID');

            const response = await fetch(`${this.adsUrl}/user/${userId}`, {
                headers: this.getAuthHeader()
            });

            if (!response.ok) {
                const error = await response.text();
                throw new Error(`Error: ${response.status} - ${error}`);
            }

            return await response.json();
        } catch (error) {
            console.error('Error:', error);
            return [];
        }
    }
    static async getAdReviews(adId) {
        try {
            const response = await fetch(`${this.adsUrl}/${adId}/reviews`, {
                headers: this.getAuthHeader()
            });
            return await response.json();
        } catch (error) {
            console.error('Error loading reviews:', error);
            return [];
        }
    }

    static async addReview(adId, reviewData) {
        try {
            const response = await fetch(`${this.adsUrl}/${adId}/reviews`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...this.getAuthHeader()
                },
                body: JSON.stringify(reviewData)
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Ошибка при добавлении отзыва');
            }

            return await response.json();
        } catch (error) {
            console.error('Error adding review:', error);
            throw error;
        }
    }
}